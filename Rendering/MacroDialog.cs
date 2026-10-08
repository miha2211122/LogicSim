using System.Numerics;
using LogicSim.Game.Core;
using Raylib_cs;
using SDColor = System.Drawing.Color;
using RRect = Raylib_cs.Rectangle;

namespace LogicSim.Game.Rendering;

public sealed class MacroDialog
{
    public bool IsOpen { get; private set; }
    public bool Confirmed { get; private set; }

    public string Name { get; set; } = "CHIP";
    public SDColor BodyColor { get; set; } = SDColor.FromArgb(45, 50, 70);
    public SDColor WireColor { get; set; } = SDColor.FromArgb(80, 220, 80);

    public MacroRecipe? Recipe { get; private set; }

    private static readonly SDColor[] BodyPalette =
    {
        SDColor.FromArgb(45, 50, 70),
        SDColor.FromArgb(70, 100, 150),
        SDColor.FromArgb(150, 60, 60),
        SDColor.FromArgb(60, 140, 90),
        SDColor.FromArgb(150, 140, 60),
        SDColor.FromArgb(140, 80, 160),
        SDColor.FromArgb(60, 140, 150),
        SDColor.FromArgb(140, 140, 140),
    };

    private static readonly SDColor[] WirePalette =
    {
        SDColor.FromArgb(80, 220, 80),
        SDColor.FromArgb(220, 80, 80),
        SDColor.FromArgb(80, 140, 220),
        SDColor.FromArgb(220, 200, 80),
        SDColor.FromArgb(200, 80, 200),
        SDColor.FromArgb(80, 220, 220),
        SDColor.FromArgb(220, 140, 60),
        SDColor.FromArgb(220, 220, 220),
    };

    private const int Width = 620;
    private const int Pad = 18;
    private const int RowH = 34;
    private const int CellSize = 24;
    private const int CellGap = 6;
    private const int BtnH = 40;
    private const int ArrowW = 32;

    private RRect _bounds;
    private bool _editingName;
    private static int _counter;

    public void Open(MacroRecipe recipe, int defaultNumber)
    {
        Recipe = recipe;
        Name = $"CHIP{defaultNumber}";
        BodyColor = SDColor.FromArgb(45, 50, 70);
        WireColor = SDColor.FromArgb(80, 220, 80);
        _editingName = false;
        Confirmed = false;
        IsOpen = true;

        int n = recipe.Inputs.Count;
        int m = recipe.Outputs.Count;
        int h = 320 + (n + m) * RowH;
        h = Math.Min(h, Raylib.GetScreenHeight() - 40);

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        _bounds = new RRect((sw - Width) / 2, (sh - h) / 2, Width, h);
    }

    public void Close()
    {
        IsOpen = false;
        Recipe = null;
        _editingName = false;
    }

    public void Update()
    {
        if (!IsOpen || Recipe is null) return;

        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { Close(); return; }

        var m = Raylib.GetMousePosition();
        bool clicked = Raylib.IsMouseButtonPressed(MouseButton.Left);

        int x = (int)_bounds.X + Pad;
        int y = (int)_bounds.Y + Pad;

        y += 30;

        var nameRect = new RRect(x, y, Width - 2 * Pad, 30);
        if (clicked)
        {
            bool inside = Raylib.CheckCollisionPointRec(m, nameRect);
            _editingName = inside;
            if (inside && Name.StartsWith("CHIP") && Name.Length <= 6) Name = "";
        }
        y += 42;

        y += 22;
        if (clicked && TryColorRow(m, x, y, BodyPalette, out var p1)) BodyColor = p1;
        y += CellSize + 14;

        y += 22;
        if (clicked && TryColorRow(m, x, y, WirePalette, out var p2)) WireColor = p2;
        y += CellSize + 20;

        // Inputs
        y += 22;
        for (int i = 0; i < Recipe.Inputs.Count; i++)
        {
            var up = new RRect(x + Width - 2 * Pad - (ArrowW + 6) * 2, y + 2, ArrowW, RowH - 4);
            var down = new RRect(x + Width - 2 * Pad - ArrowW, y + 2, ArrowW, RowH - 4);

            if (clicked)
            {
                if (Raylib.CheckCollisionPointRec(m, up) && i > 0)
                    SwapInputs(i, i - 1);
                else if (Raylib.CheckCollisionPointRec(m, down) && i < Recipe.Inputs.Count - 1)
                    SwapInputs(i, i + 1);
            }
            y += RowH;
        }
        y += 14;

        // Outputs
        y += 22;
        for (int i = 0; i < Recipe.Outputs.Count; i++)
        {
            var up = new RRect(x + Width - 2 * Pad - (ArrowW + 6) * 2, y + 2, ArrowW, RowH - 4);
            var down = new RRect(x + Width - 2 * Pad - ArrowW, y + 2, ArrowW, RowH - 4);

            if (clicked)
            {
                if (Raylib.CheckCollisionPointRec(m, up) && i > 0)
                    SwapOutputs(i, i - 1);
                else if (Raylib.CheckCollisionPointRec(m, down) && i < Recipe.Outputs.Count - 1)
                    SwapOutputs(i, i + 1);
            }
            y += RowH;
        }
        y += 14;

        int halfW = (Width - 3 * Pad) / 2;
        var createRect = new RRect(x, y, halfW, BtnH);
        var cancelRect = new RRect(x + halfW + Pad, y, halfW, BtnH);

        if (clicked)
        {
            if (Raylib.CheckCollisionPointRec(m, createRect))
            {
                Confirmed = true;
                IsOpen = false;
                return;
            }
            if (Raylib.CheckCollisionPointRec(m, cancelRect))
            {
                Close();
                return;
            }
        }

        if (_editingName)
        {
            int c;
            while ((c = Raylib.GetCharPressed()) != 0)
            {
                char ch = (char)c;
                if (Name.Length < 14 && (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'))
                    Name += ch;
            }
            if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && Name.Length > 0)
                Name = Name.Substring(0, Name.Length - 1);
        }
    }

    private void SwapInputs(int a, int b)
    {
        if (Recipe is null) return;
        (Recipe.Inputs[a], Recipe.Inputs[b]) = (Recipe.Inputs[b], Recipe.Inputs[a]);
        (Recipe.InputLabels[a], Recipe.InputLabels[b]) = (Recipe.InputLabels[b], Recipe.InputLabels[a]);
    }

    private void SwapOutputs(int a, int b)
    {
        if (Recipe is null) return;
        (Recipe.Outputs[a], Recipe.Outputs[b]) = (Recipe.Outputs[b], Recipe.Outputs[a]);
        (Recipe.OutputLabels[a], Recipe.OutputLabels[b]) = (Recipe.OutputLabels[b], Recipe.OutputLabels[a]);
    }

    private bool TryColorRow(Vector2 m, int x, int y, SDColor[] palette, out SDColor picked)
    {
        picked = default;
        for (int i = 0; i < palette.Length; i++)
        {
            var r = new RRect(x + i * (CellSize + CellGap), y, CellSize, CellSize);
            if (Raylib.CheckCollisionPointRec(m, r)) { picked = palette[i]; return true; }
        }
        return false;
    }

    public void Draw()
    {
        if (!IsOpen || Recipe is null) return;

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new Color((byte)0, (byte)0, (byte)0, (byte)160));

        Raylib.DrawRectangleRec(_bounds, new Color((byte)30, (byte)32, (byte)44, (byte)255));
        Raylib.DrawRectangleLinesEx(_bounds, 2f, new Color((byte)120, (byte)140, (byte)180, (byte)255));

        int x = (int)_bounds.X + Pad;
        int y = (int)_bounds.Y + Pad;

        Raylib.DrawText("New Macro", x, y, 26, Color.White);
        y += 30;

        var nameRect = new RRect(x, y, Width - 2 * Pad, 30);
        Raylib.DrawRectangleRec(nameRect, new Color((byte)20, (byte)22, (byte)30, (byte)255));
        Raylib.DrawRectangleLinesEx(nameRect, _editingName ? 2f : 1f,
            _editingName ? Color.Yellow : new Color((byte)80, (byte)80, (byte)100, (byte)255));

        string shown = Name;
        if (_editingName && ((int)(Raylib.GetTime() * 2) % 2 == 0)) shown += "_";
        Raylib.DrawText(shown, x + 8, y + 5, 20, Color.White);
        y += 42;

        Raylib.DrawText("Body color", x, y, 18, Color.LightGray);
        y += 22;
        DrawColorRow(x, y, BodyColor, BodyPalette);
        y += CellSize + 14;

        Raylib.DrawText("Wire color", x, y, 18, Color.LightGray);
        y += 22;
        DrawColorRow(x, y, WireColor, WirePalette);
        y += CellSize + 20;

        Raylib.DrawText($"Inputs ({Recipe.Inputs.Count})  -  order top to bottom",
            x, y, 18, Color.LightGray);
        y += 22;
        for (int i = 0; i < Recipe.Inputs.Count; i++)
        {
            Raylib.DrawText($"{i + 1}. {Recipe.InputLabels[i]}", x + 8, y + 6, 18, Color.White);
            DrawArrow(x + Width - 2 * Pad - (ArrowW + 6) * 2, y + 2, ArrowW, RowH - 4, "^");
            DrawArrow(x + Width - 2 * Pad - ArrowW, y + 2, ArrowW, RowH - 4, "v");
            y += RowH;
        }
        y += 14;

        Raylib.DrawText($"Outputs ({Recipe.Outputs.Count})  -  order top to bottom",
            x, y, 18, Color.LightGray);
        y += 22;
        for (int i = 0; i < Recipe.Outputs.Count; i++)
        {
            Raylib.DrawText($"{i + 1}. {Recipe.OutputLabels[i]}", x + 8, y + 6, 18, Color.White);
            DrawArrow(x + Width - 2 * Pad - (ArrowW + 6) * 2, y + 2, ArrowW, RowH - 4, "^");
            DrawArrow(x + Width - 2 * Pad - ArrowW, y + 2, ArrowW, RowH - 4, "v");
            y += RowH;
        }
        y += 14;

        int halfW = (Width - 3 * Pad) / 2;
        var createRect = new RRect(x, y, halfW, BtnH);
        var cancelRect = new RRect(x + halfW + Pad, y, halfW, BtnH);

        var mm = Raylib.GetMousePosition();

        bool cHover = Raylib.CheckCollisionPointRec(mm, createRect);
        Raylib.DrawRectangleRec(createRect,
            cHover ? new Color((byte)90, (byte)170, (byte)90, (byte)255)
                   : new Color((byte)70, (byte)150, (byte)70, (byte)255));
        Raylib.DrawRectangleLinesEx(createRect, 1f, new Color((byte)140, (byte)220, (byte)140, (byte)255));
        int ctw = Raylib.MeasureText("Create", 20);
        Raylib.DrawText("Create",
            (int)(createRect.X + (createRect.Width - ctw) / 2),
            (int)(createRect.Y + 10), 20, Color.White);

        bool xHover = Raylib.CheckCollisionPointRec(mm, cancelRect);
        Raylib.DrawRectangleRec(cancelRect,
            xHover ? new Color((byte)70, (byte)70, (byte)90, (byte)255)
                   : new Color((byte)50, (byte)50, (byte)65, (byte)255));
        Raylib.DrawRectangleLinesEx(cancelRect, 1f, new Color((byte)90, (byte)90, (byte)110, (byte)255));
        int xtw = Raylib.MeasureText("Cancel", 20);
        Raylib.DrawText("Cancel",
            (int)(cancelRect.X + (cancelRect.Width - xtw) / 2),
            (int)(cancelRect.Y + 10), 20, Color.White);
    }

    private static void DrawColorRow(int x, int y, SDColor selected, SDColor[] palette)
    {
        for (int i = 0; i < palette.Length; i++)
        {
            var c = palette[i];
            var rl = new Color(c.R, c.G, c.B, c.A);
            var r = new RRect(x + i * (CellSize + CellGap), y, CellSize, CellSize);
            Raylib.DrawRectangleRec(r, rl);
            Raylib.DrawRectangleLinesEx(r, selected == c ? 2f : 1f,
                selected == c ? Color.White : new Color((byte)60, (byte)60, (byte)80, (byte)255));
        }
    }

    private static void DrawArrow(int x, int y, int w, int h, string ch)
    {
        var r = new RRect(x, y, w, h);
        var m = Raylib.GetMousePosition();
        bool hover = Raylib.CheckCollisionPointRec(m, r);
        Raylib.DrawRectangleRec(r,
            hover ? new Color((byte)80, (byte)80, (byte)110, (byte)255)
                  : new Color((byte)55, (byte)58, (byte)78, (byte)255));
        Raylib.DrawRectangleLinesEx(r, 1.5f, new Color((byte)110, (byte)110, (byte)140, (byte)255));
        int tw = Raylib.MeasureText(ch, 18);
        Raylib.DrawText(ch, (int)(r.X + (r.Width - tw) / 2), (int)(r.Y + (r.Height - 18) / 2), 18, Color.White);
    }

    public static int NextNumber() => ++_counter;
}