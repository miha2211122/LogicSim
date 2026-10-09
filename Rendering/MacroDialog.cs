using LogicSim.Game.Core;
using Raylib_cs;
using System.Numerics;
using RRect = Raylib_cs.Rectangle;
using RColor = Raylib_cs.Color;
using SDColor = System.Drawing.Color;

namespace LogicSim.Game.Rendering;

public sealed class MacroDialog
{
    public bool IsOpen { get; private set; }
    public bool Confirmed { get; private set; }

    /// <summary>Если != null — редактируем существующий шаблон (не создаём новый).</summary>
    public ChipTemplate? EditingTemplate { get; private set; }

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

    private static readonly SDColor?[] PinColorCycle =
    {
        null,
        SDColor.FromArgb(80, 220, 80),
        SDColor.FromArgb(220, 80, 80),
        SDColor.FromArgb(80, 140, 220),
        SDColor.FromArgb(220, 200, 80),
        SDColor.FromArgb(200, 80, 200),
        SDColor.FromArgb(80, 220, 220),
        SDColor.FromArgb(220, 140, 60),
        SDColor.FromArgb(200, 200, 200),
    };

    private const int Width = 760;
    private const int Pad = 18;
    private const int RowH = 28;
    private const int CellSize = 22;
    private const int CellGap = 6;
    private const int BtnH = 40;
    private const int PreviewW = 340;
    private const int PreviewH = 200;
    private const int SwatchSize = 20;
    private const int StepBtnW = 28;
    private const int StepW = 70;
    private const int GridCellPx = 20;

    private RRect _bounds;
    private RRect _previewRect;
    private RRect _nameRect;
    private RRect _createRect;
    private RRect _cancelRect;
    private RRect _resetRect;

    private RRect _wMinus, _wPlus, _wValue;
    private RRect _hMinus, _hPlus, _hValue;

    private List<(RRect Row, RRect Swatch, int Index)> _inputRows = new();
    private List<(RRect Row, RRect Swatch, int Index)> _outputRows = new();

    private int _bodyRowY, _wireRowY, _bodyRowX, _wireRowX;

    private bool _editingName;
    private int _dragInputIdx = -1;
    private int _dragOutputIdx = -1;
    private static int _counter;

    public void Open(MacroRecipe recipe, int defaultNumber)
    {
        EditingTemplate = null;
        Recipe = recipe;
        Name = $"CHIP{defaultNumber}";
        BodyColor = SDColor.FromArgb(45, 50, 70);
        WireColor = SDColor.FromArgb(80, 220, 80);
        _editingName = false;
        Confirmed = false;
        IsOpen = true;
        _dragInputIdx = -1;
        _dragOutputIdx = -1;

        recipe.Width = Element.DefaultWidth;
        recipe.Height = Element.DefaultHeight;
        for (int i = 0; i < recipe.InputPinColors.Count; i++) recipe.InputPinColors[i] = null;
        for (int i = 0; i < recipe.OutputPinColors.Count; i++) recipe.OutputPinColors[i] = null;

        ComputeBoundsAndLayout();
    }

    public void OpenFromTemplate(ChipTemplate tpl)
    {
        EditingTemplate = tpl;

        var chip = tpl.Prototype;

        // Создаём «виртуальный» рецепт из существующего чипа,
        // чтобы UI работал единообразно.
        var recipe = new MacroRecipe();
        recipe.Width = chip.Width;
        recipe.Height = chip.Height;
        recipe.Inner = chip.Inner.ToList();
        recipe.InnerWires = chip.InnerWires.ToList();

        for (int i = 0; i < chip.Inputs.Count; i++)
        {
            recipe.Inputs.Add((chip.Inputs[i], new InputPin()));
            recipe.InputLabels.Add(chip.InputLabels[i]);
            recipe.InputOffsets.Add(chip.GetOffset(chip.Inputs[i]));
            recipe.InputPinColors.Add(chip.Inputs[i].Color);
        }
        for (int i = 0; i < chip.Outputs.Count; i++)
        {
            recipe.Outputs.Add((new OutputPin(), chip.Outputs[i]));
            recipe.OutputLabels.Add(chip.OutputLabels[i]);
            recipe.OutputOffsets.Add(chip.GetOffset(chip.Outputs[i]));
            recipe.OutputPinColors.Add(chip.Outputs[i].Color);
        }

        Recipe = recipe;
        Name = chip.Name;
        BodyColor = chip.BodyColor;
        WireColor = chip.WireColor;
        _editingName = false;
        Confirmed = false;
        IsOpen = true;
        _dragInputIdx = -1;
        _dragOutputIdx = -1;

        ComputeBoundsAndLayout();
    }

    private void ComputeBoundsAndLayout()
    {
        if (Recipe is null) return;

        int n = Recipe.Inputs.Count;
        int m = Recipe.Outputs.Count;

        int topBlock = 26 + 6 + 30 + 12 + PreviewH + 16;
        int listsBlock = 22 + Math.Max(n, 1) * RowH + 14;
        int h = Pad + topBlock + listsBlock + BtnH * 2 + 6 + Pad;
        h = Math.Min(h, Raylib.GetScreenHeight() - 40);

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        _bounds = new RRect((sw - Width) / 2, (sh - h) / 2, Width, h);

        LayoutRects();
    }

    public void Close()
    {
        IsOpen = false;
        Recipe = null;
        EditingTemplate = null;
        _editingName = false;
        _dragInputIdx = -1;
        _dragOutputIdx = -1;
    }

    private void LayoutRects()
    {
        int x = (int)_bounds.X + Pad;
        int y = (int)_bounds.Y + Pad;

        y += 26 + 6;
        _nameRect = new RRect(x, y, Width - Pad * 2, 30);
        y += 30 + 12;

        _previewRect = new RRect(x, y, PreviewW, PreviewH);

        int rx = x + PreviewW + Pad;

        _bodyRowX = rx;
        _bodyRowY = y + 22 + 6;
        _wireRowX = rx;
        _wireRowY = y + 22 + 6 + CellSize + 22;

        int sw2 = y + 22 + 6 + (CellSize + 22) * 2 + 4;

        _wMinus = new RRect(rx, sw2, StepBtnW, RowH);
        _wValue = new RRect(rx + StepBtnW + 4, sw2, StepW, RowH);
        _wPlus = new RRect(rx + StepBtnW + 4 + StepW + 4, sw2, StepBtnW, RowH);

        _hMinus = new RRect(rx, sw2 + RowH + 6, StepBtnW, RowH);
        _hValue = new RRect(rx + StepBtnW + 4, sw2 + RowH + 6, StepW, RowH);
        _hPlus = new RRect(rx + StepBtnW + 4 + StepW + 4, sw2 + RowH + 6, StepBtnW, RowH);

        y += PreviewH + 16;

        int halfW = (Width - Pad * 3) / 2;

        _inputRows.Clear();
        int iy = y + 22;
        if (Recipe is not null)
        {
            for (int i = 0; i < Recipe.Inputs.Count; i++)
            {
                var row = new RRect(x, iy, halfW, RowH - 4);
                var sw = new RRect(x + halfW - SwatchSize - 8, iy + 2, SwatchSize, SwatchSize);
                _inputRows.Add((row, sw, i));
                iy += RowH;
            }
        }

        _outputRows.Clear();
        int ox = x + halfW + Pad;
        int oy = y + 22;
        if (Recipe is not null)
        {
            for (int i = 0; i < Recipe.Outputs.Count; i++)
            {
                var row = new RRect(ox, oy, halfW, RowH - 4);
                var sw = new RRect(ox + halfW - SwatchSize - 8, oy + 2, SwatchSize, SwatchSize);
                _outputRows.Add((row, sw, i));
                oy += RowH;
            }
        }

        int rowsCount = Math.Max(Recipe?.Inputs.Count ?? 1, Recipe?.Outputs.Count ?? 1);
        int listsEnd = y + 22 + rowsCount * RowH + 14;

        int halfBtn = (Width - 3 * Pad) / 2;
        _resetRect = new RRect((int)_bounds.X + Pad, listsEnd, halfBtn, BtnH);
        _createRect = new RRect((int)_bounds.X + Pad + halfBtn + Pad, listsEnd, halfBtn, BtnH);
        _cancelRect = new RRect((int)_bounds.X + Pad, listsEnd + BtnH + 6, halfBtn, BtnH);
    }

    public void Update()
    {
        if (!IsOpen || Recipe is null) return;

        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { Close(); return; }

        var m = Raylib.GetMousePosition();
        bool clicked = Raylib.IsMouseButtonPressed(MouseButton.Left);
        bool down = Raylib.IsMouseButtonDown(MouseButton.Left);
        bool released = Raylib.IsMouseButtonReleased(MouseButton.Left);

        bool shift = Raylib.IsKeyDown(KeyboardKey.LeftShift)
                  || Raylib.IsKeyDown(KeyboardKey.RightShift);
        int step = shift ? 1 : 10;

        if (_dragInputIdx >= 0 || _dragOutputIdx >= 0)
        {
            if (down) { UpdatePinDrag(m); return; }
            if (released) { _dragInputIdx = -1; _dragOutputIdx = -1; return; }
        }

        if (clicked)
        {
            bool inside = Raylib.CheckCollisionPointRec(m, _nameRect);
            _editingName = inside;
            if (inside && Name.StartsWith("CHIP") && Name.Length <= 6) Name = "";
        }

        if (clicked)
        {
            if (TryColorRow(m, _bodyRowX, _bodyRowY, BodyPalette, out var p1)) BodyColor = p1;
            if (TryColorRow(m, _wireRowX, _wireRowY, WirePalette, out var p2)) WireColor = p2;
        }

        if (clicked)
        {
            if (Raylib.CheckCollisionPointRec(m, _wMinus))
                Recipe.Width = Math.Max(ChipElement.MinWidth, Recipe.Width - step);
            if (Raylib.CheckCollisionPointRec(m, _wPlus))
                Recipe.Width += step;
            if (Raylib.CheckCollisionPointRec(m, _hMinus))
            {
                int minH = Math.Max(ChipElement.MinHeight,
                    Math.Max(Recipe.Inputs.Count, Recipe.Outputs.Count) * ChipElement.MinPinSpacing);
                Recipe.Height = Math.Max(minH, Recipe.Height - step);
            }
            if (Raylib.CheckCollisionPointRec(m, _hPlus)) Recipe.Height += step;
        }

        if (clicked)
        {
            foreach (var (_, sw, idx) in _inputRows)
            {
                if (Raylib.CheckCollisionPointRec(m, sw))
                {
                    Recipe.InputPinColors[idx] = NextColor(Recipe.InputPinColors[idx]);
                    return;
                }
            }
            foreach (var (_, sw, idx) in _outputRows)
            {
                if (Raylib.CheckCollisionPointRec(m, sw))
                {
                    Recipe.OutputPinColors[idx] = NextColor(Recipe.OutputPinColors[idx]);
                    return;
                }
            }
        }

        if (clicked && Raylib.CheckCollisionPointRec(m, _previewRect))
            if (TryStartPinDrag(m)) return;

        if (clicked)
        {
            if (Raylib.CheckCollisionPointRec(m, _resetRect))
            {
                for (int i = 0; i < Recipe.InputOffsets.Count; i++)
                    Recipe.InputOffsets[i] = ChipElement.DefaultOffset(Recipe.InputOffsets.Count, i);
                for (int i = 0; i < Recipe.OutputOffsets.Count; i++)
                    Recipe.OutputOffsets[i] = ChipElement.DefaultOffset(Recipe.OutputOffsets.Count, i);
                for (int i = 0; i < Recipe.InputPinColors.Count; i++) Recipe.InputPinColors[i] = null;
                for (int i = 0; i < Recipe.OutputPinColors.Count; i++) Recipe.OutputPinColors[i] = null;
                Recipe.Width = Element.DefaultWidth;
                Recipe.Height = Element.DefaultHeight;
                return;
            }
            if (Raylib.CheckCollisionPointRec(m, _createRect))
            {
                Confirmed = true;
                IsOpen = false;
                return;
            }
            if (Raylib.CheckCollisionPointRec(m, _cancelRect)) { Close(); return; }
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

    private static SDColor? NextColor(SDColor? current)
    {
        int idx = 0;
        if (current.HasValue)
        {
            for (int i = 0; i < PinColorCycle.Length; i++)
                if (PinColorCycle[i] == current.Value) { idx = i; break; }
        }
        return PinColorCycle[(idx + 1) % PinColorCycle.Length];
    }

    private bool TryStartPinDrag(Vector2 m)
    {
        if (Recipe is null) return false;
        var (cx, cy, w, h, scale) = PreviewChipRect();
        float pinR = Layout.VisualPinRadius * scale;
        float hitR = pinR + 6f;

        for (int i = 0; i < Recipe.Inputs.Count; i++)
        {
            float py = cy + Recipe.InputOffsets[i] * h;
            if (Vector2.Distance(m, new Vector2(cx, py)) <= hitR) { _dragInputIdx = i; return true; }
        }
        for (int i = 0; i < Recipe.Outputs.Count; i++)
        {
            float py = cy + Recipe.OutputOffsets[i] * h;
            if (Vector2.Distance(m, new Vector2(cx + w, py)) <= hitR) { _dragOutputIdx = i; return true; }
        }
        return false;
    }

    private void UpdatePinDrag(Vector2 m)
    {
        if (Recipe is null) return;
        var (_, cy, _, h, _) = PreviewChipRect();

        if (_dragInputIdx >= 0)
            Recipe.InputOffsets[_dragInputIdx] = Clamp((m.Y - cy) / h);
        if (_dragOutputIdx >= 0)
            Recipe.OutputOffsets[_dragOutputIdx] = Clamp((m.Y - cy) / h);
    }

    private static float Clamp(float t) => t < 0.08f ? 0.08f : (t > 0.92f ? 0.92f : t);

    private (float cx, float cy, float w, float h, float scale) PreviewChipRect()
    {
        if (Recipe is null) return (0, 0, 0, 0, 1f);

        const int TopLabelH = 22;
        const int BottomHintH = 20;
        float availW = _previewRect.Width;
        float availH = _previewRect.Height - TopLabelH - BottomHintH;

        float scale = MathF.Min((float)availW / Recipe.Width, availH / Recipe.Height) * 0.9f;
        if (scale <= 0f) scale = 1f;

        float w = Recipe.Width * scale;
        float h = Recipe.Height * scale;
        float cx = _previewRect.X + (_previewRect.Width - w) / 2f;
        float cy = _previewRect.Y + TopLabelH + (availH - h) / 2f;
        return (cx, cy, w, h, scale);
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
            new RColor((byte)0, (byte)0, (byte)0, (byte)160));
        Raylib.DrawRectangleRec(_bounds, new RColor((byte)30, (byte)32, (byte)44, (byte)255));
        Raylib.DrawRectangleLinesEx(_bounds, 2f, new RColor((byte)120, (byte)140, (byte)180, (byte)255));

        int x = (int)_bounds.X + Pad;
        int y = (int)_bounds.Y + Pad;

        string title = EditingTemplate is null ? "New Macro" : $"Edit Macro: {EditingTemplate.Name}";
        Raylib.DrawText(title, x, y, 26, RColor.White);
        y += 26 + 6;

        Raylib.DrawRectangleRec(_nameRect, new RColor((byte)20, (byte)22, (byte)30, (byte)255));
        Raylib.DrawRectangleLinesEx(_nameRect, _editingName ? 2f : 1f,
            _editingName ? RColor.Yellow : new RColor((byte)80, (byte)80, (byte)100, (byte)255));
        string shown = Name;
        if (_editingName && ((int)(Raylib.GetTime() * 2) % 2 == 0)) shown += "_";
        Raylib.DrawText(shown, x + 8, y + 5, 20, RColor.White);

        DrawPreview();

        Raylib.DrawText("Body", _bodyRowX, _bodyRowY - 22, 16, RColor.LightGray);
        DrawColorRow(_bodyRowX, _bodyRowY, BodyColor, BodyPalette);

        Raylib.DrawText("Wire", _wireRowX, _wireRowY - 22, 16, RColor.LightGray);
        DrawColorRow(_wireRowX, _wireRowY, WireColor, WirePalette);

        var m = Raylib.GetMousePosition();

        DrawStepper("Width", _wMinus, _wValue, _wPlus, Recipe.Width, m);
        DrawStepper("Height", _hMinus, _hValue, _hPlus, Recipe.Height, m);

        int listsY = _inputRows.Count > 0
            ? (int)_inputRows[0].Row.Y - 22
            : (int)_previewRect.Y + PreviewH + 16;

        Raylib.DrawText($"Inputs ({Recipe.Inputs.Count})", x, listsY, 16, RColor.LightGray);
        int ox = _outputRows.Count > 0 ? (int)_outputRows[0].Row.X : x + 300;
        Raylib.DrawText($"Outputs ({Recipe.Outputs.Count})", ox, listsY, 16, RColor.LightGray);

        for (int i = 0; i < _inputRows.Count; i++)
        {
            var (row, sw, idx) = _inputRows[i];
            DrawPinRow(row, Recipe.InputLabels[idx], m, idx == _dragInputIdx,
                Recipe.InputOffsets[idx], Recipe.InputPinColors[idx], sw);
        }
        for (int i = 0; i < _outputRows.Count; i++)
        {
            var (row, sw, idx) = _outputRows[i];
            DrawPinRow(row, Recipe.OutputLabels[idx], m, idx == _dragOutputIdx,
                Recipe.OutputOffsets[idx], Recipe.OutputPinColors[idx], sw);
        }

        DrawButton(_resetRect, "Reset", m, new RColor((byte)120, (byte)120, (byte)150, (byte)255));
        string okLabel = EditingTemplate is null ? "Create" : "Apply";
        RColor okColor = EditingTemplate is null
            ? new RColor((byte)70, (byte)150, (byte)70, (byte)255)
            : new RColor((byte)80, (byte)120, (byte)200, (byte)255);
        DrawButton(_createRect, okLabel, m, okColor);
        DrawButton(_cancelRect, "Cancel", m, new RColor((byte)50, (byte)50, (byte)65, (byte)255));
    }

    private static void DrawStepper(string label, RRect minus, RRect value, RRect plus, int val, Vector2 m)
    {
        Raylib.DrawText(label, (int)minus.X, (int)minus.Y - 20, 14, RColor.LightGray);
        DrawSmallBtn(minus, "-", m);
        DrawSmallBtn(plus, "+", m);

        Raylib.DrawRectangleRec(value, new RColor((byte)20, (byte)22, (byte)30, (byte)255));
        Raylib.DrawRectangleLinesEx(value, 1f, new RColor((byte)70, (byte)80, (byte)100, (byte)255));
        string s = val.ToString();
        int tw = Raylib.MeasureText(s, 18);
        Raylib.DrawText(s, (int)(value.X + (value.Width - tw) / 2),
            (int)(value.Y + (value.Height - 18) / 2), 18, RColor.White);
    }

    private static void DrawSmallBtn(RRect r, string label, Vector2 m)
    {
        bool hover = Raylib.CheckCollisionPointRec(m, r);
        Raylib.DrawRectangleRec(r, hover
            ? new RColor((byte)80, (byte)90, (byte)120, (byte)255)
            : new RColor((byte)50, (byte)55, (byte)75, (byte)255));
        Raylib.DrawRectangleLinesEx(r, 1f, new RColor((byte)110, (byte)120, (byte)150, (byte)255));
        int tw = Raylib.MeasureText(label, 18);
        Raylib.DrawText(label, (int)(r.X + (r.Width - tw) / 2), (int)(r.Y + (r.Height - 18) / 2), 18, RColor.White);
    }

    private void DrawPreview()
    {
        Raylib.DrawRectangleRec(_previewRect, new RColor((byte)18, (byte)20, (byte)28, (byte)255));
        Raylib.DrawRectangleLinesEx(_previewRect, 1f, new RColor((byte)70, (byte)80, (byte)110, (byte)255));

        if (Recipe is null) return;

        string sizeLbl = $"{Recipe.Width} x {Recipe.Height} px";
        int sw = Raylib.MeasureText(sizeLbl, 16);
        Raylib.DrawText(sizeLbl,
            (int)(_previewRect.X + (_previewRect.Width - sw) / 2),
            (int)_previewRect.Y + 4,
            16, new RColor((byte)180, (byte)200, (byte)230, (byte)255));

        var (cx, cy, w, h, scale) = PreviewChipRect();
        var body = new RColor(BodyColor.R, BodyColor.G, BodyColor.B, (byte)255);

        Raylib.DrawRectangle((int)cx, (int)cy, (int)w, (int)h, body);

        var gridColor = new RColor((byte)255, (byte)255, (byte)255, (byte)30);
        float cellPx = GridCellPx * scale;

        if (cellPx >= 4f)
        {
            for (float gx = cx + cellPx; gx < cx + w - 0.5f; gx += cellPx)
                Raylib.DrawLine((int)gx, (int)cy, (int)gx, (int)(cy + h), gridColor);
            for (float gy = cy + cellPx; gy < cy + h - 0.5f; gy += cellPx)
                Raylib.DrawLine((int)cx, (int)gy, (int)(cx + w), (int)gy, gridColor);
        }

        Raylib.DrawRectangleLines((int)cx, (int)cy, (int)w, (int)h, RColor.White);

        int tw = Raylib.MeasureText(Name, 22);
        Raylib.DrawText(Name, (int)(cx + (w - tw) / 2), (int)(cy + h / 2 - 11), 22, RColor.White);

        float pinR = Layout.VisualPinRadius * scale;
        var defaultColor = new RColor(WireColor.R, WireColor.G, WireColor.B, (byte)255);
        var lblColor = new RColor((byte)200, (byte)230, (byte)255, (byte)220);

        for (int i = 0; i < Recipe.Inputs.Count; i++)
        {
            float py = cy + Recipe.InputOffsets[i] * h;
            bool dragged = i == _dragInputIdx;
            var c = Recipe.InputPinColors[i] is SDColor ic
                ? new RColor(ic.R, ic.G, ic.B, (byte)255) : defaultColor;
            Raylib.DrawCircle((int)cx, (int)py, pinR, c);
            if (dragged) Raylib.DrawCircleLines((int)cx, (int)py, pinR + 3, RColor.Yellow);
            Raylib.DrawText(Recipe.InputLabels[i], (int)(cx + pinR + 6), (int)(py - 8), 14, lblColor);
        }
        for (int i = 0; i < Recipe.Outputs.Count; i++)
        {
            float py = cy + Recipe.OutputOffsets[i] * h;
            bool dragged = i == _dragOutputIdx;
            var c = Recipe.OutputPinColors[i] is SDColor oc
                ? new RColor(oc.R, oc.G, oc.B, (byte)255) : defaultColor;
            Raylib.DrawCircle((int)(cx + w), (int)py, pinR, c);
            if (dragged) Raylib.DrawCircleLines((int)(cx + w), (int)py, pinR + 3, RColor.Yellow);
            string lbl = Recipe.OutputLabels[i];
            int lw = Raylib.MeasureText(lbl, 14);
            Raylib.DrawText(lbl, (int)(cx + w - pinR - 6 - lw), (int)(py - 8), 14, lblColor);
        }

        int lx = (int)_previewRect.X + 8;
        int ly = (int)(_previewRect.Y + _previewRect.Height - 20);
        var rulerBg = new RColor((byte)40, (byte)45, (byte)60, (byte)255);
        var rulerLine = new RColor((byte)180, (byte)200, (byte)230, (byte)255);

        int rcw = (int)(GridCellPx * scale);
        if (rcw < 2) rcw = 2;
        int totalW = rcw * 3;
        Raylib.DrawRectangle(lx, ly, totalW, 10, rulerBg);
        for (int i = 0; i <= 3; i++)
            Raylib.DrawLine(lx + i * rcw, ly, lx + i * rcw, ly + 10, rulerLine);
        Raylib.DrawText($"{GridCellPx}px", lx + totalW + 6, ly - 3, 12,
            new RColor((byte)150, (byte)160, (byte)180, (byte)255));

        Raylib.DrawText("drag pin along the side",
            (int)_previewRect.X + (int)_previewRect.Width - 168,
            (int)(_previewRect.Y + _previewRect.Height - 20),
            12, new RColor((byte)120, (byte)130, (byte)150, (byte)255));
    }

    private static void DrawColorRow(int x, int y, SDColor selected, SDColor[] palette)
    {
        for (int i = 0; i < palette.Length; i++)
        {
            var c = palette[i];
            var rl = new RColor(c.R, c.G, c.B, c.A);
            var r = new RRect(x + i * (CellSize + CellGap), y, CellSize, CellSize);
            Raylib.DrawRectangleRec(r, rl);
            Raylib.DrawRectangleLinesEx(r, selected == c ? 2f : 1f,
                selected == c ? RColor.White : new RColor((byte)60, (byte)60, (byte)80, (byte)255));
        }
    }

    private static void DrawPinRow(RRect row, string label, Vector2 m, bool dragged,
                                   float offset, SDColor? pinColor, RRect swatch)
    {
        bool hover = Raylib.CheckCollisionPointRec(m, row);
        var bg = dragged
            ? new RColor((byte)60, (byte)60, (byte)90, (byte)255)
            : hover ? new RColor((byte)50, (byte)55, (byte)75, (byte)255)
                    : new RColor((byte)35, (byte)38, (byte)52, (byte)255);

        Raylib.DrawRectangleRec(row, bg);
        Raylib.DrawRectangleLinesEx(row, dragged ? 2f : 1f,
            dragged ? RColor.Yellow : new RColor((byte)70, (byte)80, (byte)110, (byte)255));

        Raylib.DrawText(label, (int)row.X + 8, (int)(row.Y + (row.Height - 18) / 2), 18, RColor.White);

        string y = $"y:{offset:0.00}";
        int yw = Raylib.MeasureText(y, 12);
        Raylib.DrawText(y, (int)(row.X + row.Width - yw - SwatchSize - 18),
            (int)(row.Y + (row.Height - 12) / 2), 12,
            new RColor((byte)150, (byte)160, (byte)180, (byte)255));

        bool swatchHover = Raylib.CheckCollisionPointRec(m, swatch);
        if (pinColor is SDColor pc)
        {
            Raylib.DrawRectangleRec(swatch, new RColor(pc.R, pc.G, pc.B, (byte)255));
        }
        else
        {
            Raylib.DrawRectangleRec(swatch, new RColor((byte)30, (byte)30, (byte)40, (byte)255));
            Raylib.DrawLine((int)swatch.X + 3, (int)swatch.Y + (int)swatch.Height - 3,
                            (int)swatch.X + (int)swatch.Width - 3, (int)swatch.Y + 3,
                            new RColor((byte)180, (byte)90, (byte)90, (byte)255));
        }
        Raylib.DrawRectangleLinesEx(swatch, swatchHover ? 2f : 1f,
            swatchHover ? RColor.White : new RColor((byte)100, (byte)110, (byte)140, (byte)255));
    }

    private static void DrawButton(RRect r, string label, Vector2 m, RColor accent)
    {
        bool hover = Raylib.CheckCollisionPointRec(m, r);
        Raylib.DrawRectangleRec(r, hover ? accent : new RColor((byte)50, (byte)50, (byte)70, (byte)255));
        Raylib.DrawRectangleLinesEx(r, 1f, new RColor((byte)120, (byte)120, (byte)150, (byte)255));
        int tw = Raylib.MeasureText(label, 18);
        Raylib.DrawText(label, (int)(r.X + (r.Width - tw) / 2), (int)(r.Y + (r.Height - 18) / 2), 18, RColor.White);
    }

    public static int NextNumber() => ++_counter;
}