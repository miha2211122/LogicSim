using System.Numerics;
using LogicSim.Game.Core;
using Raylib_cs;
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

            var baseColor = owner1.WireColor;
            var color = wire.From.Value ? ToRaylib(baseColor) : Darken(baseColor, 0.45f);

            var points = new List<Vector2> { p1.Value };
            points.AddRange(wire.Waypoints);
            points.Add(p2.Value);

            for (int i = 0; i + 1 < points.Count; i++)
                Raylib.DrawLineEx(points[i], points[i + 1], 3f, color);

            foreach (var wp in wire.Waypoints)
            {
                Raylib.DrawCircleV(wp, 5, color);
                Raylib.DrawCircleLinesV(wp, 5, Color.White);
            }
        }

        foreach (var el in circuit.Elements)
            if (layout.TryGet(el, out var p))
                DrawElement(el, p.X, p.Y);
    }

    public void DrawOverlay(Editor editor, Camera2D camera)
    {
        var layout = editor.Layout;

        foreach (var el in editor.Selection)
        {
            if (!layout.TryGet(el, out var p)) continue;
            Raylib.DrawRectangleLinesEx(
                new Rectangle(p.X - 4, p.Y - 4, Layout.W + 8, Layout.H + 8),
                2f, new Color((byte)255, (byte)230, (byte)90, (byte)255));
        }

        if (editor.SelectionRect() is { } sr)
        {
            var fill = editor.SelectionAdditive
                ? new Color((byte)120, (byte)220, (byte)120, (byte)60)
                : new Color((byte)120, (byte)180, (byte)255, (byte)60);
            var line = editor.SelectionAdditive
                ? new Color((byte)180, (byte)240, (byte)180, (byte)255)
                : new Color((byte)180, (byte)210, (byte)255, (byte)255);

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
            {
                Raylib.DrawCircleV(wp, 5, rc);
                Raylib.DrawCircleLinesV(wp, 5, Color.White);
            }
            Raylib.DrawCircleV(editor.MouseWorld, 6, rc);
        }

        if (editor.Hover is { } h)
        {
            var pos = layout.PinPosition(h.Element, h.Pin);
            if (pos is { } pp)
            {
                var color = h.IsInput
                    ? new Color((byte)120, (byte)200, (byte)255, (byte)255)
                    : new Color((byte)255, (byte)200, (byte)120, (byte)255);
                Raylib.DrawCircleLinesV(pp, Layout.HitPinRadius, color);

                int idx = Layout.PinIndex(h.Element, h.Pin, h.IsInput);
                var labels = h.IsInput ? h.Element.InputLabels : h.Element.OutputLabels;
                if (idx >= 0 && idx < labels.Count)
                {
                    string lbl = labels[idx];
                    int lw = Raylib.MeasureText(lbl, 18);
                    int lx = (int)(pp.X + (h.IsInput ? -lw - 16 : 16));
                    int ly = (int)(pp.Y - 9);
                    Raylib.DrawText(lbl, lx, ly, 18, Color.Yellow);
                }
            }
        }

        DrawHolograms(editor);
    }

    private void DrawHolograms(Editor editor)
    {
        if (Raylib.GetMousePosition().Y >= Raylib.GetScreenHeight() - Palette.BarHeight) return;
        if (editor.IsReadOnly) return;

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
        var ghostBody = new Color(body.R, body.G, body.B, (byte)110);
        var ghostLine = new Color((byte)255, (byte)255, (byte)255, (byte)160);
        int vpr = (int)Layout.VisualPinRadius;

        for (int i = 0; i < count; i++)
        {
            int x = (int)editor.MouseWorld.X - Layout.W / 2;
            int y = (int)editor.MouseWorld.Y - Layout.H / 2 + i * (Layout.H + gap);

            Raylib.DrawRectangle(x, y, Layout.W, Layout.H, ghostBody);
            Raylib.DrawRectangleLines(x, y, Layout.W, Layout.H, ghostLine);

            int tw = Raylib.MeasureText(name, 22);
            Raylib.DrawText(name, x + (Layout.W - tw) / 2, y + Layout.H / 2 - 12, 22, ghostLine);
            Raylib.DrawText((i + 1).ToString(), x + 6, y + 4, 16, Color.Yellow);

            int pinCount = name == "NAND" ? 2 : 1;
            int outCount = name == "OUT" ? 0 : 1;

            for (int k = 0; k < pinCount; k++)
                Raylib.DrawCircleLines(x, Layout.PinY(y, pinCount, k), vpr, ghostLine);
            for (int k = 0; k < outCount; k++)
                Raylib.DrawCircleLines(x + Layout.W, Layout.PinY(y, outCount, k), vpr, ghostLine);
        }
    }

    private void DrawElement(Element el, int x, int y)
    {
        Raylib.DrawRectangle(x, y, Layout.W, Layout.H, ToRaylib(el.BodyColor));
        Raylib.DrawRectangleLines(x, y, Layout.W, Layout.H, Color.White);

        int tw = Raylib.MeasureText(el.Name, 22);
        Raylib.DrawText(el.Name, x + (Layout.W - tw) / 2, y + Layout.H / 2 - 12, 22, Color.White);

        int vpr = (int)Layout.VisualPinRadius;

        for (int i = 0; i < el.Inputs.Count; i++)
        {
            int py = Layout.PinY(y, el.Inputs.Count, i);
            Raylib.DrawCircle(x, py, vpr, el.Inputs[i].Value ? Color.Green : Color.Red);
            if (i < el.InputLabels.Count)
                Raylib.DrawText(el.InputLabels[i], x + vpr + 4, py - 9, 14, Color.LightGray);
        }

        for (int i = 0; i < el.Outputs.Count; i++)
        {
            int py = Layout.PinY(y, el.Outputs.Count, i);
            Raylib.DrawCircle(x + Layout.W, py, vpr, el.Outputs[i].Value ? Color.Green : Color.Red);
            if (i < el.OutputLabels.Count)
            {
                string lbl = el.OutputLabels[i];
                int lw = Raylib.MeasureText(lbl, 14);
                Raylib.DrawText(lbl, x + Layout.W - vpr - lw - 4, py - 9, 14, Color.LightGray);
            }
        }
    }

    private static Color ToRaylib(SDColor c) => new(c.R, c.G, c.B, c.A);
    private static Color Darken(SDColor c, float f) =>
        new((byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f), c.A);
}