using LogicSim.Game.Core;
using Raylib_cs;
using System.Numerics;
using RColor = Raylib_cs.Color;
using SDColor = System.Drawing.Color;

namespace LogicSim.Game.Rendering;

public sealed class GameRenderer
{
    public void Draw(Circuit circuit, Layout layout)
    {
        foreach (var wire in circuit.Wires)
        {
            var owner1 = circuit.Elements.FirstOrDefault(e => e.Outputs.Contains(wire.From));
            var owner2 = circuit.Elements.FirstOrDefault(e => e.Inputs.Contains(wire.To));
            if (owner1 is null || owner2 is null) continue;

            var p1 = layout.PinPosition(owner1, wire.From);
            var p2 = layout.PinPosition(owner2, wire.To);
            if (p1 is null || p2 is null) continue;

            var points = new List<Vector2> { p1.Value };
            points.AddRange(wire.Waypoints);
            points.Add(p2.Value);

            var net = circuit.NetStateOf(wire);
            var baseColor = wire.From.Color ?? owner1.WireColor;

            switch (net)
            {
                case LogicState.Conflict:
                    DrawHazardPolyline(points, 3f);
                    break;
                case LogicState.HighZ:
                    DrawDashedPolyline(points, 3f, new RColor((byte)150, (byte)150, (byte)160, (byte)255));
                    break;
                default:
                    {
                        var color = net == LogicState.One ? ToRaylib(baseColor) : Darken(baseColor, 0.45f);
                        for (int i = 0; i + 1 < points.Count; i++)
                            Raylib.DrawLineEx(points[i], points[i + 1], 3f, color);
                        break;
                    }
            }

            foreach (var wp in wire.Waypoints)
            {
                switch (net)
                {
                    case LogicState.Conflict:
                        Raylib.DrawCircleV(wp, 5, new RColor((byte)220, (byte)40, (byte)40, (byte)255));
                        break;
                    case LogicState.HighZ:
                        Raylib.DrawCircleV(wp, 5, new RColor((byte)150, (byte)150, (byte)160, (byte)255));
                        break;
                    default:
                        {
                            var color = net == LogicState.One ? ToRaylib(baseColor) : Darken(baseColor, 0.45f);
                            Raylib.DrawCircleV(wp, 5, color);
                            break;
                        }
                }
            }
        }

        foreach (var el in circuit.Elements)
            if (layout.TryGet(el, out var p))
                DrawElement(el, p.X, p.Y, circuit, layout);
    }

    public void DrawOverlay(Editor editor, Camera2D camera)
    {
        var layout = editor.Layout;
        foreach (var el in editor.Selection)
        {
            if (!layout.TryGet(el, out var p)) continue;
            Raylib.DrawRectangleLinesEx(
                new Rectangle(p.X - 4, p.Y - 4, el.Width + 8, el.Height + 8),
                2f, new RColor((byte)255, (byte)230, (byte)90, (byte)255));
        }

        if (editor.SelectionRect() is { } sr)
        {
            var fill = editor.SelectionAdditive
                ? new RColor((byte)120, (byte)220, (byte)120, (byte)60)
                : new RColor((byte)120, (byte)180, (byte)255, (byte)60);
            var line = editor.SelectionAdditive
                ? new RColor((byte)180, (byte)240, (byte)180, (byte)255)
                : new RColor((byte)180, (byte)210, (byte)255, (byte)255);

            Raylib.DrawRectangleRec(sr, fill);
            Raylib.DrawRectangleLinesEx(sr, 1.5f, line);
        }

        if (editor.WireStart is { } start && editor.WirePreviewColor is { } c)
        {
            var rc = ToRaylib(c);
            var pts = new List<Vector2> { start };
            pts.AddRange(editor.PendingWaypoints);
            pts.Add(editor.MouseWorld);

            for (int i = 0; i + 1 < pts.Count; i++)
                Raylib.DrawLineEx(pts[i], pts[i + 1], 3f, rc);

            foreach (var wp in editor.PendingWaypoints)
                Raylib.DrawCircleV(wp, 5, rc);

            Raylib.DrawCircleV(editor.MouseWorld, 6, rc);
        }

        if (editor.Hover is { } h)
        {
            var pos = layout.PinPosition(h.Element, h.Pin);
            if (pos is { } pp)
            {
                var color = h.IsInput
                    ? new RColor((byte)120, (byte)200, (byte)255, (byte)255)
                    : new RColor((byte)255, (byte)200, (byte)120, (byte)255);
                Raylib.DrawCircleLinesV(pp, Layout.HitPinRadius, color);

                int idx = Layout.PinIndex(h.Element, h.Pin, h.IsInput);
                var labels = h.IsInput ? h.Element.InputLabels : h.Element.OutputLabels;
                if (idx >= 0 && idx < labels.Count)
                {
                    string lbl = labels[idx];
                    int lw = Raylib.MeasureText(lbl, 18);
                    int lx = (int)(pp.X + (h.IsInput ? -lw - 16 : 16));
                    int ly = (int)(pp.Y - 9);
                    Raylib.DrawText(lbl, lx, ly, 18, RColor.Yellow);
                }
            }
        }

        if (editor.HoveredWire is { } hw)
        {
            var net = editor.Circuit.NetStateAt(hw.To);
            string label = StateLabel(net);
            int fontSize = 20;
            int tw = Raylib.MeasureText(label, fontSize);
            int tx = (int)editor.MouseWorld.X + 16;
            int ty = (int)editor.MouseWorld.Y - 34;

            Raylib.DrawRectangle(tx - 6, ty - 4, tw + 12, fontSize + 8,
                new RColor((byte)20, (byte)20, (byte)20, (byte)220));
            Raylib.DrawRectangleLines(tx - 6, ty - 4, tw + 12, fontSize + 8,
                new RColor((byte)200, (byte)180, (byte)40, (byte)255));
            Raylib.DrawText(label, tx, ty, fontSize, RColor.Yellow);
        }

        DrawPasteHologram(editor);
        DrawHolograms(editor);
    }

    private void DrawPasteHologram(Editor editor)
    {
        if (editor.PasteHologram is not { } hol) return;
        if (hol.Elements.Count == 0) return;

        var offset = editor.MouseWorld - editor.PasteHologramCenter;
        var ghostFill = new RColor((byte)255, (byte)255, (byte)255, (byte)50);
        var ghostLine = new RColor((byte)200, (byte)230, (byte)255, (byte)210);
        int vpr = (int)Layout.VisualPinRadius;

        for (int i = 0; i < hol.Elements.Count; i++)
        {
            var el = hol.Elements[i];
            var p = hol.Positions[i] + offset;
            int x = (int)p.X, y = (int)p.Y;

            for (int k = 0; k < el.Inputs.Count; k++)
            {
                int py = el is ChipElement ce
                    ? y + (int)(ce.GetOffset(el.Inputs[k]) * ce.Height)
                    : Layout.PinY(y, el.Height, el.Inputs.Count, k);
                Raylib.DrawCircleLines(x, py, vpr, ghostLine);
            }
            for (int k = 0; k < el.Outputs.Count; k++)
            {
                int py = el is ChipElement ce
                    ? y + (int)(ce.GetOffset(el.Outputs[k]) * ce.Height)
                    : Layout.PinY(y, el.Height, el.Outputs.Count, k);
                Raylib.DrawCircleLines(x + el.Width, py, vpr, ghostLine);
            }

            Raylib.DrawRectangle(x, y, el.Width, el.Height, ghostFill);
            Raylib.DrawRectangleLines(x, y, el.Width, el.Height, ghostLine);

            int tw = Raylib.MeasureText(el.Name, 22);
            Raylib.DrawText(el.Name, x + (el.Width - tw) / 2, y + el.Height / 2 - 12, 22, ghostLine);
        }
    }

    private void DrawHolograms(Editor editor)
    {
        if (Raylib.GetMousePosition().Y >= Raylib.GetScreenHeight() - Palette.BarHeight) return;
        if (editor.IsReadOnly) return;
        if (editor.PasteHologram is not null) return;

        int count = editor.Palette.PendingCount;
        if (count <= 0) return;

        string? name = null;
        SDColor body = SDColor.FromArgb(45, 50, 70);

        if (editor.Palette.SelectedTemplate is { } tpl) { name = tpl.Name; body = tpl.BodyColor; }
        else name = editor.Palette.SelectedTool switch
        {
            Palette.Tool.In => "IN",
            Palette.Tool.Out => "OUT",
            Palette.Tool.Nand => "NAND",
            _ => null
        };
        if (name is null) return;

        const int gap = 24;
        var ghostBody = new RColor(body.R, body.G, body.B, (byte)110);
        var ghostLine = new RColor((byte)255, (byte)255, (byte)255, (byte)160);
        int vpr = (int)Layout.VisualPinRadius;

        for (int i = 0; i < count; i++)
        {
            int x = (int)editor.MouseWorld.X - Element.DefaultWidth / 2;
            int y = (int)editor.MouseWorld.Y - Element.DefaultHeight / 2 + i * (Element.DefaultHeight + gap);

            int pinCount = name == "NAND" ? 2 : 1;
            int outCount = name == "OUT" ? 0 : 1;

            for (int k = 0; k < pinCount; k++)
                Raylib.DrawCircleLines(x, Layout.PinY(y, Element.DefaultHeight, pinCount, k), vpr, ghostLine);
            for (int k = 0; k < outCount; k++)
                Raylib.DrawCircleLines(x + Element.DefaultWidth,
                    Layout.PinY(y, Element.DefaultHeight, outCount, k), vpr, ghostLine);

            Raylib.DrawRectangle(x, y, Element.DefaultWidth, Element.DefaultHeight, ghostBody);
            Raylib.DrawRectangleLines(x, y, Element.DefaultWidth, Element.DefaultHeight, ghostLine);

            int tw = Raylib.MeasureText(name, 22);
            Raylib.DrawText(name, x + (Element.DefaultWidth - tw) / 2,
                y + Element.DefaultHeight / 2 - 12, 22, ghostLine);
            Raylib.DrawText((i + 1).ToString(), x + 6, y + 4, 16, RColor.Yellow);
        }
    }

    private void DrawElement(Element el, int x, int y, Circuit circuit, Layout layout)
    {
        int vpr = (int)Layout.VisualPinRadius;

        for (int i = 0; i < el.Inputs.Count; i++)
        {
            int py = layout.PinScreenY(el, el.Inputs[i], isInput: true);
            Raylib.DrawCircle(x, py, vpr, InputPinColor(el.Inputs[i], circuit));
        }
        for (int i = 0; i < el.Outputs.Count; i++)
        {
            int py = layout.PinScreenY(el, el.Outputs[i], isInput: false);
            Raylib.DrawCircle(x + el.Width, py, vpr, OutputPinColor(el.Outputs[i], el));
        }

        Raylib.DrawRectangle(x, y, el.Width, el.Height, ToRaylib(el.BodyColor));
        Raylib.DrawRectangleLines(x, y, el.Width, el.Height, RColor.White);

        int tw = Raylib.MeasureText(el.Name, 22);
        Raylib.DrawText(el.Name, x + (el.Width - tw) / 2, y + el.Height / 2 - 12, 22, RColor.White);

        for (int i = 0; i < el.Inputs.Count; i++)
        {
            int py = layout.PinScreenY(el, el.Inputs[i], isInput: true);
            if (i < el.InputLabels.Count)
                Raylib.DrawText(el.InputLabels[i], x + vpr + 4, py - 9, 14, RColor.LightGray);
        }
        for (int i = 0; i < el.Outputs.Count; i++)
        {
            int py = layout.PinScreenY(el, el.Outputs[i], isInput: false);
            if (i < el.OutputLabels.Count)
            {
                string lbl = el.OutputLabels[i];
                int lw = Raylib.MeasureText(lbl, 14);
                Raylib.DrawText(lbl, x + el.Width - vpr - lw - 4, py - 9, 14, RColor.LightGray);
            }
        }
    }

    private static RColor OutputPinColor(OutputPin pin, Element owner)
    {
        var baseColor = pin.Color ?? owner.WireColor;
        switch (pin.State)
        {
            case LogicState.One: return ToRaylib(baseColor);
            case LogicState.Zero: return Darken(baseColor, 0.45f);
            case LogicState.HighZ: return new RColor((byte)140, (byte)140, (byte)150, (byte)255);
            case LogicState.Conflict: return ConflictPulse();
            default: return RColor.White;
        }
    }

    private static RColor InputPinColor(InputPin pin, Circuit circuit)
    {
        Element? driver = null;
        Pin? driverPin = null;
        foreach (var w in circuit.Wires)
        {
            if (!ReferenceEquals(w.To, pin)) continue;
            driver = circuit.OwnerOf(w.From);
            driverPin = w.From;
            if (driver is not null) break;
        }

        if (driver is not null)
        {
            var baseColor = driverPin?.Color ?? driver.WireColor;
            var net = circuit.NetStateAt(pin);
            return net switch
            {
                LogicState.One => ToRaylib(baseColor),
                LogicState.Zero => Darken(baseColor, 0.45f),
                LogicState.HighZ => new RColor((byte)140, (byte)140, (byte)150, (byte)255),
                LogicState.Conflict => ConflictPulse(),
                _ => RColor.White
            };
        }

        // Нет драйвера: у пина может быть свой цвет — используем его как «базу».
        var fallback = pin.Color;
        if (fallback.HasValue)
        {
            return pin.State switch
            {
                LogicState.One => ToRaylib(fallback.Value),
                LogicState.Zero => Darken(fallback.Value, 0.45f),
                LogicState.HighZ => new RColor((byte)140, (byte)140, (byte)150, (byte)255),
                LogicState.Conflict => ConflictPulse(),
                _ => RColor.White
            };
        }

        return pin.State switch
        {
            LogicState.One => new RColor((byte)80, (byte)220, (byte)80, (byte)255),
            LogicState.Zero => new RColor((byte)220, (byte)60, (byte)60, (byte)255),
            LogicState.HighZ => new RColor((byte)140, (byte)140, (byte)150, (byte)255),
            LogicState.Conflict => ConflictPulse(),
            _ => RColor.White
        };
    }

    private static RColor ConflictPulse()
        => ((int)(Raylib.GetTime() * 6) % 2 == 0)
            ? new RColor((byte)240, (byte)60, (byte)60, (byte)255)
            : new RColor((byte)20, (byte)20, (byte)20, (byte)255);

    private static string StateLabel(LogicState s) => s switch
    {
        LogicState.Zero => "0",
        LogicState.One => "1",
        LogicState.HighZ => "Z",
        LogicState.Conflict => "SHORT (X)",
        _ => "?"
    };

    private static void DrawHazardPolyline(List<Vector2> points, float thickness, float dashLen = 10f)
    {
        var black = new RColor((byte)0, (byte)0, (byte)0, (byte)255);
        var red = new RColor((byte)230, (byte)30, (byte)30, (byte)255);

        for (int i = 0; i + 1 < points.Count; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            var d = b - a;
            float len = d.Length();
            if (len < 0.01f) continue;
            var dir = d / len;

            float pos = 0f;
            bool isBlack = true;
            while (pos < len)
            {
                float chunk = MathF.Min(dashLen, len - pos);
                Raylib.DrawLineEx(a + dir * pos, a + dir * (pos + chunk), thickness, isBlack ? black : red);
                pos += chunk;
                isBlack = !isBlack;
            }
        }
    }

    private static void DrawDashedPolyline(List<Vector2> points, float thickness, RColor color,
                                           float dashLen = 10f, float gapLen = 6f)
    {
        for (int i = 0; i + 1 < points.Count; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            var d = b - a;
            float len = d.Length();
            if (len < 0.01f) continue;
            var dir = d / len;

            float pos = 0f;
            bool on = true;
            while (pos < len)
            {
                float chunk = MathF.Min(on ? dashLen : gapLen, len - pos);
                if (on)
                    Raylib.DrawLineEx(a + dir * pos, a + dir * (pos + chunk), thickness, color);
                pos += chunk;
                on = !on;
            }
        }
    }

    private static RColor ToRaylib(SDColor c) => new(c.R, c.G, c.B, c.A);
    private static RColor Darken(SDColor c, float f) =>
        new((byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f), c.A);
}