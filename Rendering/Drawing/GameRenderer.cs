using LogicSim.Game.Core;
using Raylib_cs;
using System.Numerics;
using RColor = Raylib_cs.Color;
using SDColor = System.Drawing.Color;

namespace LogicSim.Game.Rendering;

public sealed class GameRenderer
{
    private RenderTexture2D? _ghostTarget;

    public void Draw(Circuit circuit, Layout layout, LayerSet? layers = null, Camera2D? camera = null)
    {
        if (camera is not null && layers is not null && layers.Layers.Any(l => !l.Visible))
            DrawHiddenGhost(circuit, layout, layers, camera.Value);

        DrawPass(circuit, layout, layers, ghost: false);
    }

    private void DrawPass(Circuit circuit, Layout layout, LayerSet? layers, bool ghost)
    {
        bool Visible(Element el) => layers is null || layers.IsVisible(el.Layer) != ghost;

        bool WireVisible(Wire w)
        {
            if (layers is null) return !ghost;
            var from = circuit.OwnerOf(w.From);
            var to = circuit.OwnerOf(w.To);
            if (from is null || to is null) return false;
            if (ghost)
                return !layers.IsVisible(w.Layer) || Visible(from) || Visible(to);
            return layers.IsVisible(w.Layer) && Visible(from) && Visible(to);
        }

        foreach (var wire in circuit.Wires)
        {
            if (!WireVisible(wire)) continue;
            var owner1 = circuit.OwnerOf(wire.From);
            var owner2 = circuit.OwnerOf(wire.To);
            if (owner1 is null || owner2 is null) continue;

            var p1 = layout.PinPosition(owner1, wire.From);
            var p2 = layout.PinPosition(owner2, wire.To);
            if (p1 is null || p2 is null) continue;

            var rawPoints = new List<Vector2> { p1.Value };
            rawPoints.AddRange(wire.Waypoints);
            rawPoints.Add(p2.Value);

            if (!wire.IsValid) continue;

            var net = circuit.NetStateOf(wire);
            var baseColor = wire.From.Color ?? owner1.WireColor;

            if (wire.IsBus)
            {
                int strips = Math.Clamp(Math.Max(wire.From.BitWidth, wire.To.BitWidth), 2, 32);
                var states = wire.To.BitWidth > 1 ? wire.To.BusStates : wire.From.BusStates;
                DrawRibbonCable(rawPoints, states, baseColor, strips);
                continue;
            }

            var color = net switch
            {
                LogicState.One => ToRaylib(baseColor),
                LogicState.Zero => Darken(baseColor, 0.45f),
                LogicState.HighZ => new RColor((byte)140, (byte)140, (byte)150, (byte)255),
                LogicState.Conflict => ConflictPulse(),
                _ => Darken(baseColor, 0.45f)
            };
            for (int i = 0; i + 1 < rawPoints.Count; i++)
                if (net == LogicState.HighZ)
                    DrawDashedWire(rawPoints[i], rawPoints[i + 1], 3f, color);
                else
                    Raylib.DrawLineEx(rawPoints[i], rawPoints[i + 1], 3f, color);
        }

        foreach (var el in circuit.Elements)
            if (Visible(el) && layout.TryGet(el, out var p))
                DrawElement(el, p.X, p.Y, circuit, layout);
    }

    private void DrawHiddenGhost(Circuit circuit, Layout layout, LayerSet layers, Camera2D camera)
    {
        int w = Raylib.GetScreenWidth();
        int h = Raylib.GetScreenHeight();
        if (w <= 0 || h <= 0) return;

        if (_ghostTarget is null || _ghostTarget.Value.Texture.Width != w || _ghostTarget.Value.Texture.Height != h)
        {
            if (_ghostTarget is not null) Raylib.UnloadRenderTexture(_ghostTarget.Value);
            _ghostTarget = Raylib.LoadRenderTexture(w, h);
        }

        Raylib.EndMode2D();
        Raylib.BeginTextureMode(_ghostTarget.Value);
        Raylib.ClearBackground(new Color(0, 0, 0, 0));
        Raylib.BeginMode2D(camera);

        DrawPass(circuit, layout, layers, ghost: true);
        Raylib.EndMode2D();
        Raylib.EndTextureMode();

        var tex = _ghostTarget.Value.Texture;
        var src = new Rectangle(0, 0, tex.Width, -tex.Height);
        Raylib.DrawTextureRec(tex, src, Vector2.Zero,
            new Color((byte)255, (byte)255, (byte)255, (byte)64));
        Raylib.BeginMode2D(camera);
    }

    private List<Vector2> RemoveDuplicatePoints(List<Vector2> points)
    {
        var result = new List<Vector2>();
        if (points.Count == 0) return result;
        result.Add(points[0]);
        for (int i = 1; i < points.Count; i++)
        {
            if (Vector2.DistanceSquared(points[i], result[^1]) > 0.01f)
                result.Add(points[i]);
        }
        return result;
    }

    private void DrawRibbonCable(List<Vector2> points, LogicState[] states, SDColor baseColor, int bitWidth)
    {
        points = RemoveDuplicatePoints(points);
        if (points.Count < 2) return;

        float stripThickness = 2f;
        int strips = Math.Max(2, bitWidth);
        float totalThickness = strips * stripThickness;

        var bgColor = new RColor((byte)15, (byte)20, (byte)15, (byte)255);
        for (int i = 0; i + 1 < points.Count; i++)
            Raylib.DrawLineEx(points[i], points[i + 1], totalThickness + 2f, bgColor);

        var colorOn = ToRaylib(baseColor);
        // Темно-зеленый для нулей, но чтобы НЕ сливался с серым Z
        var colorOff = new RColor((byte)40, (byte)140, (byte)40, (byte)255);
        var colorZ = new RColor((byte)60, (byte)60, (byte)70, (byte)255);

        for (int s = 0; s < strips; s++)
        {
            float offset = (s - strips / 2f) * stripThickness + stripThickness / 2f;

            var state = s < states.Length ? states[s] : LogicState.HighZ;

            RColor baseC;
            if (state == LogicState.One) baseC = colorOn;
            else if (state == LogicState.Zero) baseC = colorOff;
            else if (state == LogicState.Conflict) baseC = (s % 2 == 0) ? RColor.Red : RColor.Black;
            else baseC = colorZ;

            RColor c = baseC;
            if (s % 2 == 1)
            {
                c = new RColor((byte)(baseC.R * 0.7f), (byte)(baseC.G * 0.7f), (byte)(baseC.B * 0.7f), (byte)255);
            }

            var stripPoints = new List<Vector2>();
            stripPoints.Add(points[0] + Vector2.Normalize(new Vector2(-(points[1] - points[0]).Y, (points[1] - points[0]).X)) * offset);

            for (int i = 1; i < points.Count - 1; i++)
            {
                var prev = points[i - 1];
                var curr = points[i];
                var next = points[i + 1];

                var d1 = curr - prev;
                var d2 = next - curr;
                float len1 = d1.Length();
                float len2 = d2.Length();

                if (len1 < 0.01f || len2 < 0.01f) continue;

                d1 /= len1;
                d2 /= len2;

                var n1 = new Vector2(-d1.Y, d1.X);
                var n2 = new Vector2(-d2.Y, d2.X);

                float bevelDistance = Math.Min(10f, Math.Min(len1 / 2f, len2 / 2f));
                var pStart = curr - d1 * bevelDistance;
                var pEnd = curr + d2 * bevelDistance;

                stripPoints.Add(pStart + n1 * offset);
                stripPoints.Add(pEnd + n2 * offset);
            }

            var dLast = points[^1] - points[^2];
            stripPoints.Add(points[^1] + Vector2.Normalize(new Vector2(-dLast.Y, dLast.X)) * offset);

            for (int i = 0; i + 1 < stripPoints.Count; i++)
            {
                Raylib.DrawLineEx(stripPoints[i], stripPoints[i + 1], stripThickness, c);
            }
        }
    }

    public void DrawOverlay(Editor editor, Camera2D camera)
    {
        var layout = editor.Layout;
        foreach (var el in editor.Selection)
        {
            if (!editor.IsElementVisible(el)) continue;
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
            var rawPts = new List<Vector2> { start };
            rawPts.AddRange(editor.PendingWaypoints);
            rawPts.Add(editor.MouseWorld);

            // Ширина провода определяется пином-источником, а не типом элемента:
            // шинный выход макроса (ChipElement) тоже должен тянуться лентой.
            int strips = Math.Clamp(editor.WireStartPin?.BitWidth ?? 1, 1, 32);
            if (strips > 1)
            {
                var dummyStates = new LogicState[32];
                for (int i = 0; i < 32; i++) dummyStates[i] = LogicState.Zero;

                DrawRibbonCable(rawPts, dummyStates, c, strips);
                Raylib.DrawCircleV(editor.MouseWorld, 6, ToRaylib(c));
            }
            else
            {
                var rc = ToRaylib(c);
                for (int i = 0; i + 1 < rawPts.Count; i++)
                    Raylib.DrawLineEx(rawPts[i], rawPts[i + 1], 3f, rc);

                Raylib.DrawCircleV(editor.MouseWorld, 6, rc);
            }
        }

        if (editor.Hover is { } h)
        {
            var pos = layout.PinPosition(h.Element, h.Pin);
            if (pos is { } pp)
            {
                var color = h.IsInput
                    ? new RColor((byte)120, (byte)200, (byte)255, (byte)255)
                    : new RColor((byte)255, (byte)200, (byte)120, (byte)255);

                if (h.Pin.BitWidth > 1)
                {
                    float pinW = 12f;
                    float pinH = h.Pin.BitWidth * 2f;
                    if (pinH < 4f) pinH = 4f;
                    Raylib.DrawRectangleLines((int)(pp.X - pinW / 2 - 1), (int)(pp.Y - pinH / 2 - 1),
                        (int)pinW + 2, (int)pinH + 2, color);
                }
                else
                {
                    Raylib.DrawCircleLinesV(pp, Layout.HitPinRadius, color);
                }

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

                bool isBus = el.Inputs[k].BitWidth > 1;
                if (isBus)
                {
                    float pinW = 8f;
                    float pinH = el.Inputs[k].BitWidth * 2f;
                    if (pinH < 4f) pinH = 4f;
                    Raylib.DrawRectangleLines(x - (int)pinW / 2, py - (int)pinH / 2, (int)pinW, (int)pinH, ghostLine);
                }
                else
                {
                    Raylib.DrawCircleLines(x, py, vpr, ghostLine);
                }
            }
            for (int k = 0; k < el.Outputs.Count; k++)
            {
                int py = el is ChipElement ce
                    ? y + (int)(ce.GetOffset(el.Outputs[k]) * ce.Height)
                    : Layout.PinY(y, el.Height, el.Outputs.Count, k);

                bool isBus = el.Outputs[k].BitWidth > 1;
                if (isBus)
                {
                    float pinW = 8f;
                    float pinH = el.Outputs[k].BitWidth * 2f;
                    if (pinH < 4f) pinH = 4f;
                    Raylib.DrawRectangleLines(x + el.Width - (int)pinW / 2, py - (int)pinH / 2, (int)pinW, (int)pinH, ghostLine);
                }
                else
                {
                    Raylib.DrawCircleLines(x + el.Width, py, vpr, ghostLine);
                }
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

        Element? previewEl = null;
        if (editor.Palette.SelectedTemplate is { } tpl)
        {
            previewEl = tpl.Prototype.Clone();
        }
        else
        {
            previewEl = Palette.CreateElement(editor.Palette.SelectedTool);
        }

        if (previewEl is null) return;

        string name = previewEl.Name;
        SDColor body = previewEl.BodyColor;
        int width = previewEl.Width;
        int height = previewEl.Height;
        int pinCount = previewEl.Inputs.Count;
        int outCount = previewEl.Outputs.Count;

        const int gap = 24;
        var ghostBody = new RColor(body.R, body.G, body.B, (byte)110);
        var ghostLine = new RColor((byte)255, (byte)255, (byte)255, (byte)160);
        int vpr = (int)Layout.VisualPinRadius;

        for (int i = 0; i < count; i++)
        {
            int x = (int)editor.MouseWorld.X - width / 2;
            int y = (int)editor.MouseWorld.Y - height / 2 + i * (height + gap);

            for (int k = 0; k < pinCount; k++)
            {
                int py = previewEl is ChipElement ce
                    ? y + (int)(ce.GetOffset(previewEl.Inputs[k]) * height)
                    : Layout.PinY(y, height, pinCount, k);

                bool isBus = previewEl.Inputs[k].BitWidth > 1;
                if (isBus)
                {
                    float pinW = 8f;
                    float pinH = previewEl.Inputs[k].BitWidth * 2f;
                    if (pinH < 4f) pinH = 4f;
                    Raylib.DrawRectangleLines(x - (int)pinW / 2, py - (int)pinH / 2, (int)pinW, (int)pinH, ghostLine);
                }
                else
                {
                    Raylib.DrawCircleLines(x, py, vpr, ghostLine);
                }
            }

            for (int k = 0; k < outCount; k++)
            {
                int py = previewEl is ChipElement ce
                    ? y + (int)(ce.GetOffset(previewEl.Outputs[k]) * height)
                    : Layout.PinY(y, height, outCount, k);

                bool isBus = previewEl.Outputs[k].BitWidth > 1;
                if (isBus)
                {
                    float pinW = 8f;
                    float pinH = previewEl.Outputs[k].BitWidth * 2f;
                    if (pinH < 4f) pinH = 4f;
                    Raylib.DrawRectangleLines(x + width - (int)pinW / 2, py - (int)pinH / 2, (int)pinW, (int)pinH, ghostLine);
                }
                else
                {
                    Raylib.DrawCircleLines(x + width, py, vpr, ghostLine);
                }
            }

            Raylib.DrawRectangle(x, y, width, height, ghostBody);
            Raylib.DrawRectangleLines(x, y, width, height, ghostLine);

            int tw = Raylib.MeasureText(name, 22);
            Raylib.DrawText(name, x + (width - tw) / 2, y + height / 2 - 12, 22, ghostLine);
            Raylib.DrawText((i + 1).ToString(), x + 6, y + 4, 16, RColor.Yellow);
        }
    }

    private void DrawElement(Element el, int x, int y, Circuit circuit, Layout layout)
    {
        int vpr = (int)Layout.VisualPinRadius;

        for (int i = 0; i < el.Inputs.Count; i++)
        {
            int py = layout.PinScreenY(el, el.Inputs[i], isInput: true);
            var color = InputPinColor(el.Inputs[i], circuit);

            bool isBusPin = el.Inputs[i].BitWidth > 1;
            if (isBusPin)
            {
                float pinW = 8f;
                float pinH = el.Inputs[i].BitWidth * 2f;
                if (pinH < 4f) pinH = 4f;
                Raylib.DrawRectangle(x - (int)pinW / 2, py - (int)pinH / 2, (int)pinW, (int)pinH, color);
            }
            else
            {
                Raylib.DrawCircle(x, py, vpr, color);
            }
        }
        for (int i = 0; i < el.Outputs.Count; i++)
        {
            int py = layout.PinScreenY(el, el.Outputs[i], isInput: false);
            var color = OutputPinColor(el.Outputs[i], el);

            bool isBusPin = el.Outputs[i].BitWidth > 1;
            if (isBusPin)
            {
                float pinW = 8f;
                float pinH = el.Outputs[i].BitWidth * 2f;
                if (pinH < 4f) pinH = 4f;
                Raylib.DrawRectangle(x + el.Width - (int)pinW / 2, py - (int)pinH / 2, (int)pinW, (int)pinH, color);
            }
            else
            {
                Raylib.DrawCircle(x + el.Width, py, vpr, color);
            }
        }

        Raylib.DrawRectangle(x, y, el.Width, el.Height, ToRaylib(el.BodyColor));
        Raylib.DrawRectangleLines(x, y, el.Width, el.Height, RColor.White);

        int tw = Raylib.MeasureText(el.Name, 22);
        Raylib.DrawText(el.Name, x + (el.Width - tw) / 2, y + el.Height / 2 - 12, 22, RColor.White);

        string? tag = el switch
        {
            ClockElement clock => $"{clock.PeriodTicks}t",
            DelayElement delay => $"{delay.DelayTicks}t",
            _ => null
        };
        if (tag is not null)
        {
            int lw = Raylib.MeasureText(tag, 13);
            Raylib.DrawText(tag, x + (el.Width - lw) / 2, y + el.Height / 2 + 14, 13, RColor.Yellow);
        }

        for (int i = 0; i < el.Inputs.Count; i++)
        {
            int py = layout.PinScreenY(el, el.Inputs[i], isInput: true);
            if (i < el.InputLabels.Count)
                Raylib.DrawText(el.InputLabels[i], x + vpr + 8, py - 9, 14, RColor.LightGray);
        }
        for (int i = 0; i < el.Outputs.Count; i++)
        {
            int py = layout.PinScreenY(el, el.Outputs[i], isInput: false);
            if (i < el.OutputLabels.Count)
            {
                string lbl = el.OutputLabels[i];
                int lw = Raylib.MeasureText(lbl, 14);
                Raylib.DrawText(lbl, x + el.Width - vpr - lw - 8, py - 9, 14, RColor.LightGray);
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

    /// <summary>Пунктир для цепи в состоянии High-Z.</summary>
    private static void DrawDashedWire(Vector2 a, Vector2 b, float thickness, RColor color)
    {
        const float dash = 9f;
        const float gap = 6f;

        float len = Vector2.Distance(a, b);
        if (len < 0.01f) return;
        if (len <= dash)
        {
            Raylib.DrawLineEx(a, b, thickness, color);
            return;
        }

        var dir = (b - a) / len;
        for (float t = 0f; t < len; t += dash + gap)
        {
            var from = a + dir * t;
            var to = a + dir * Math.Min(t + dash, len);
            Raylib.DrawLineEx(from, to, thickness, color);
        }
    }

    private static RColor ConflictPulse()
        => ((int)(Raylib.GetTime() * 6) % 2 == 0)
            ? new RColor((byte)240, (byte)60, (byte)60, (byte)255)
            : new RColor((byte)110, (byte)28, (byte)28, (byte)255);

    private static string StateLabel(LogicState s) => s switch
    {
        LogicState.Zero => "0",
        LogicState.One => "1",
        LogicState.HighZ => "Z",
        LogicState.Conflict => "SHORT (X)",
        _ => "?"
    };

    private static RColor ToRaylib(SDColor c) => new(c.R, c.G, c.B, c.A);
    private static RColor Darken(SDColor c, float f) =>
        new((byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f), c.A);
}