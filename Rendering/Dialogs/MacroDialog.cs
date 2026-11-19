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

    public ChipTemplate? EditingTemplate { get; private set; }

    public string Name { get; set; } = "";
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

    private const int Width = 800;
    private const int Pad = 24;
    private const int RowH = 30;
    private const int CellSize = 24;
    private const int CellGap = 8;
    private const int BtnH = 44;
    private const int PreviewW = 360;
    private const int PreviewH = 220;
    private const int MinVisiblePinRows = 4;
    private const int SwatchSize = 22;
    private const int StepBtnW = 30;
    private const int StepW = 76;
    private const int GridCellPx = 20;
    private const int BtnGap = 12;

    private const int PreviewTopLabelH = 26;
    private const int PreviewBottomHintH = 22;
    private const float MinPreviewPinStep = 16f;

    private static readonly RColor DialogBg = new(22, 24, 32, 250);
    private static readonly RColor DialogBorderOuter = new(60, 80, 120, 80);
    private static readonly RColor DialogBorderMid = new(80, 100, 150, 130);
    private static readonly RColor DialogBorder = new(110, 140, 190, 255);
    private static readonly RColor FieldBg = new(16, 18, 26, 255);
    private static readonly RColor FieldBgError = new(36, 20, 20, 255);
    private static readonly RColor FieldBorder = new(70, 85, 115, 255);
    private static readonly RColor FieldBorderFocus = new(130, 180, 250, 255);
    private static readonly RColor FieldBorderError = new(220, 90, 90, 255);
    private static readonly RColor PreviewBg = new(14, 16, 22, 255);
    private static readonly RColor AccentGreen = new(80, 190, 100, 255);
    private static readonly RColor AccentGreenHover = new(100, 210, 120, 255);
    private static readonly RColor AccentBlue = new(90, 150, 230, 255);
    private static readonly RColor AccentBlueHover = new(110, 170, 250, 255);
    private static readonly RColor ButtonBg = new(48, 54, 72, 255);
    private static readonly RColor ButtonBgHover = new(62, 70, 92, 255);
    private static readonly RColor ButtonBorder = new(100, 115, 150, 255);
    private static readonly RColor ButtonDisabledBg = new(36, 40, 52, 255);
    private static readonly RColor ButtonDisabledBorder = new(60, 70, 90, 255);
    private static readonly RColor TextPrimary = new(240, 244, 252, 255);
    private static readonly RColor TextSecondary = new(170, 185, 210, 255);
    private static readonly RColor TextMuted = new(110, 125, 150, 255);
    private static readonly RColor SectionLabel = new(150, 175, 215, 255);
    private static readonly RColor SectionLine = new(52, 62, 85, 255);
    private static readonly RColor DimBackdrop = new(0, 0, 0, 180);

    private RRect _bounds;
    private RRect _previewRect;
    private int _previewH;
    private RRect _nameRect;
    private RRect _createRect;
    private RRect _cancelRect;
    private RRect _resetRect;

    private RRect _wMinus, _wPlus, _wValue;
    private RRect _hMinus, _hPlus, _hValue;

    private List<(RRect Row, RRect Swatch, int Index)> _inputRows = new();
    private List<(RRect Row, RRect Swatch, int Index)> _outputRows = new();
    private RRect _listViewport;
    private int _scrollY;
    private int _maxScroll;

    private int _previewScrollY;
    private int _previewMaxScroll;
    private float _previewViewTop;
    private float _previewViewH;

    private int _bodyRowY, _wireRowY, _bodyRowX, _wireRowX;
    private int _nameLabelY;
    private int _colorsSectionY;
    private int _sizeSectionY;
    private int _inputsLabelY, _outputsLabelY, _outputsLabelX;
    private int _rightPanelX, _rightPanelW;

    private bool _editingName;
    private int _dragInputIdx = -1;
    private int _dragOutputIdx = -1;
    private static int _counter;

    private bool _nameError;
    private float _nameErrorTimer;

    public void Open(MacroRecipe recipe, int defaultNumber)
    {
        _ = defaultNumber;

        EditingTemplate = null;
        Recipe = recipe;
        Name = "";
        BodyColor = SDColor.FromArgb(45, 50, 70);
        WireColor = SDColor.FromArgb(80, 220, 80);
        _editingName = true;
        Confirmed = false;
        IsOpen = true;
        _dragInputIdx = -1;
        _dragOutputIdx = -1;
        _nameError = false;
        _nameErrorTimer = 0f;

        for (int i = 0; i < recipe.InputPinColors.Count; i++) recipe.InputPinColors[i] = null;
        for (int i = 0; i < recipe.OutputPinColors.Count; i++) recipe.OutputPinColors[i] = null;

        _scrollY = 0;
        _previewScrollY = 0;
        ComputeBoundsAndLayout();
    }

    public void OpenFromTemplate(ChipTemplate tpl)
    {
        EditingTemplate = tpl;

        var chip = tpl.Prototype;

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
            recipe.Outputs.Add((new InputPin(), chip.Outputs[i]));
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
        _nameError = false;
        _nameErrorTimer = 0f;

        _scrollY = 0;
        _previewScrollY = 0;
        ComputeBoundsAndLayout();
    }

    private void ComputeBoundsAndLayout()
    {
        if (Recipe is null) return;

        int n = Recipe.Inputs.Count;
        int m = Recipe.Outputs.Count;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();

        // На невысоком окне превью не должно съедать весь список пинов.
        _previewH = Math.Clamp(sh - 290 - MinVisiblePinRows * RowH, 180, PreviewH);

        int topBlock = 26 + 8 + 18 + 34 + 16 + _previewH + 16;
        int listsBlock = 24 + Math.Max(Math.Max(n, m), 1) * RowH + 16;
        int h = Pad + topBlock + listsBlock + BtnH + Pad;
        int minH = Pad + topBlock + RowH + 16 + BtnH + Pad;
        h = Math.Clamp(h, minH, Math.Max(minH, sh - 40));

        _bounds = new RRect((sw - Width) / 2, (sh - h) / 2, Width, h);

        LayoutRects();
        _ = PreviewChipRect();
    }

    public void Close()
    {
        IsOpen = false;
        Recipe = null;
        EditingTemplate = null;
        _editingName = false;
        _dragInputIdx = -1;
        _dragOutputIdx = -1;
        _nameError = false;
        _nameErrorTimer = 0f;
    }

    private void LayoutRects()
    {
        int x = (int)_bounds.X + Pad;
        int y = (int)_bounds.Y + Pad;

        y += 26 + 8;

        _nameLabelY = y;
        y += 18;
        _nameRect = new RRect(x, y, Width - Pad * 2, 34);
        y += 34 + 16;

        _previewRect = new RRect(x, y, PreviewW, _previewH);

        int rx = x + PreviewW + Pad;
        _rightPanelX = rx;
        _rightPanelW = (int)(_bounds.X + _bounds.Width - Pad) - rx;

        _colorsSectionY = y;
        _bodyRowX = rx;
        _bodyRowY = y + 26 + 6;
        _wireRowX = rx;
        _wireRowY = y + 26 + 6 + CellSize + 24;

        _sizeSectionY = y + 26 + 6 + (CellSize + 24) * 2 - 4;
        int sw2 = _sizeSectionY + 26;
        int groupW = StepBtnW * 2 + StepW + 8;
        if (_previewH < 216)
        {
            // Узкое окно: W и H в одну строку, чтобы не залезть на список пинов.
            int hx = rx + groupW + 20;
            _wMinus = new RRect(rx, sw2, StepBtnW, RowH);
            _wValue = new RRect(rx + StepBtnW + 4, sw2, StepW, RowH);
            _wPlus = new RRect(rx + StepBtnW + 4 + StepW + 4, sw2, StepBtnW, RowH);
            _hMinus = new RRect(hx, sw2, StepBtnW, RowH);
            _hValue = new RRect(hx + StepBtnW + 4, sw2, StepW, RowH);
            _hPlus = new RRect(hx + StepBtnW + 4 + StepW + 4, sw2, StepBtnW, RowH);
        }
        else
        {
            _wMinus = new RRect(rx, sw2, StepBtnW, RowH);
            _wValue = new RRect(rx + StepBtnW + 4, sw2, StepW, RowH);
            _wPlus = new RRect(rx + StepBtnW + 4 + StepW + 4, sw2, StepBtnW, RowH);

            _hMinus = new RRect(rx, sw2 + RowH + 6, StepBtnW, RowH);
            _hValue = new RRect(rx + StepBtnW + 4, sw2 + RowH + 6, StepW, RowH);
            _hPlus = new RRect(rx + StepBtnW + 4 + StepW + 4, sw2 + RowH + 6, StepBtnW, RowH);
        }

        y += _previewH + 16;

        int halfW = (Width - Pad * 3) / 2;

        int rowsCount = Math.Max(Recipe?.Inputs.Count ?? 0, Recipe?.Outputs.Count ?? 0);
        int buttonsY = (int)(_bounds.Y + _bounds.Height) - Pad - BtnH;
        int listTop = y + 24;
        int visibleRows = Math.Max(1, (buttonsY - 16 - listTop) / RowH);
        _listViewport = new RRect(x, listTop, Width - Pad * 2, visibleRows * RowH);
        _maxScroll = Math.Max(0, (rowsCount - visibleRows) * RowH);
        _scrollY = Math.Clamp(_scrollY, 0, _maxScroll);

        _inputsLabelY = y + 4;
        _outputsLabelY = y + 4;
        _outputsLabelX = x + halfW + Pad;

        _inputRows.Clear();
        if (Recipe is not null)
        {
            for (int i = 0; i < Recipe.Inputs.Count; i++)
            {
                int ry = listTop + i * RowH - _scrollY;
                if (ry + RowH <= listTop || ry >= listTop + _listViewport.Height) continue;
                var row = new RRect(x, ry, halfW, RowH - 4);
                var sw = new RRect(x + halfW - SwatchSize - 8, ry + (RowH - 4 - SwatchSize) / 2, SwatchSize, SwatchSize);
                _inputRows.Add((row, sw, i));
            }
        }

        _outputRows.Clear();
        if (Recipe is not null)
        {
            for (int i = 0; i < Recipe.Outputs.Count; i++)
            {
                int ry = listTop + i * RowH - _scrollY;
                if (ry + RowH <= listTop || ry >= listTop + _listViewport.Height) continue;
                var row = new RRect(_outputsLabelX, ry, halfW, RowH - 4);
                var sw = new RRect(_outputsLabelX + halfW - SwatchSize - 8, ry + (RowH - 4 - SwatchSize) / 2, SwatchSize, SwatchSize);
                _outputRows.Add((row, sw, i));
            }
        }

        int btnW = (Width - Pad * 2 - BtnGap * 2) / 3;
        _resetRect = new RRect(x, buttonsY, btnW, BtnH);
        _cancelRect = new RRect(x + btnW + BtnGap, buttonsY, btnW, BtnH);
        _createRect = new RRect(x + (btnW + BtnGap) * 2, buttonsY, btnW, BtnH);
    }

    private static bool IsAllowedNameChar(char ch)
        => (ch >= 'a' && ch <= 'z')
        || (ch >= 'A' && ch <= 'Z')
        || (ch >= '0' && ch <= '9')
        || ch == '_'
        || ch == '-';

    public void Update()
    {
        if (!IsOpen || Recipe is null) return;

        if (_nameErrorTimer > 0f)
        {
            _nameErrorTimer -= Raylib.GetFrameTime();
            if (_nameErrorTimer <= 0f) _nameError = false;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { Close(); return; }

        var m = Raylib.GetMousePosition();

        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0f && _previewMaxScroll > 0 && Raylib.CheckCollisionPointRec(m, _previewRect))
        {
            int next = Math.Clamp(_previewScrollY - (int)(wheel * RowH * 2), 0, _previewMaxScroll);
            if (next != _previewScrollY) { _previewScrollY = next; LayoutRects(); }
        }
        else if (wheel != 0f && _maxScroll > 0 && Raylib.CheckCollisionPointRec(m, _bounds))
        {
            int next = Math.Clamp(_scrollY - (int)(wheel * RowH * 3), 0, _maxScroll);
            next = (next / RowH) * RowH;
            if (next != _scrollY) { _scrollY = next; LayoutRects(); }
        }

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
            _editingName = Raylib.CheckCollisionPointRec(m, _nameRect);
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
                int maxLabelW = 0;
                foreach (var lbl in Recipe.InputLabels) maxLabelW = Math.Max(maxLabelW, Raylib.MeasureText(lbl, 14));
                foreach (var lbl in Recipe.OutputLabels) maxLabelW = Math.Max(maxLabelW, Raylib.MeasureText(lbl, 14));
                Recipe.Width = Math.Max(130, maxLabelW * 2 + 60);
                Recipe.Height = Math.Max(80, Math.Max(Recipe.Inputs.Count, Recipe.Outputs.Count) * ChipElement.MinPinSpacing + 20);
                return;
            }
            if (Raylib.CheckCollisionPointRec(m, _createRect))
            {
                TryConfirm();
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
                if (Name.Length < 14 && IsAllowedNameChar(ch))
                {
                    Name += ch;
                    _nameError = false;
                    _nameErrorTimer = 0f;
                }
            }
            if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && Name.Length > 0)
                Name = Name.Substring(0, Name.Length - 1);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter))
            TryConfirm();
    }

    private void TryConfirm()
    {
        Name = Name.Trim();

        if (string.IsNullOrWhiteSpace(Name))
        {
            _nameError = true;
            _nameErrorTimer = 1.5f;
            return;
        }

        Confirmed = true;
        IsOpen = false;
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
        float chipTop = cy - _previewScrollY;
        float pinR = Layout.VisualPinRadius * scale;

        for (int i = 0; i < Recipe.Inputs.Count; i++)
        {
            float py = chipTop + Recipe.InputOffsets[i] * h;
            if (py < _previewViewTop - 8f || py > _previewViewTop + _previewViewH + 8f) continue;
            int bitWidth = Recipe.Inputs[i].ext.BitWidth;
            float hitR = bitWidth > 1 ? Math.Max(10f, bitWidth * 2f) * scale / 2f + 6f : pinR + 6f;

            if (Vector2.Distance(m, new Vector2(cx, py)) <= hitR) { _dragInputIdx = i; return true; }
        }
        for (int i = 0; i < Recipe.Outputs.Count; i++)
        {
            float py = chipTop + Recipe.OutputOffsets[i] * h;
            if (py < _previewViewTop - 8f || py > _previewViewTop + _previewViewH + 8f) continue;
            int bitWidth = Recipe.Outputs[i].ext.BitWidth;
            float hitR = bitWidth > 1 ? Math.Max(10f, bitWidth * 2f) * scale / 2f + 6f : pinR + 6f;

            if (Vector2.Distance(m, new Vector2(cx + w, py)) <= hitR) { _dragOutputIdx = i; return true; }
        }
        return false;
    }

    private void UpdatePinDrag(Vector2 m)
    {
        if (Recipe is null) return;
        var (_, cy, _, h, _) = PreviewChipRect();

        if (_dragInputIdx >= 0)
            Recipe.InputOffsets[_dragInputIdx] = Clamp(isInput: true, (m.Y + _previewScrollY - cy) / h);
        if (_dragOutputIdx >= 0)
            Recipe.OutputOffsets[_dragOutputIdx] = Clamp(isInput: false, (m.Y + _previewScrollY - cy) / h);
    }

    private float Clamp(bool isInput, float t)
    {
        int count = isInput ? Recipe?.Inputs.Count ?? 1 : Recipe?.Outputs.Count ?? 1;
        float margin = ChipElement.PinMargin(count);
        return t < margin ? margin : (t > 1f - margin ? 1f - margin : t);
    }

    private (float cx, float cy, float w, float h, float scale) PreviewChipRect()
    {
        if (Recipe is null) return (0, 0, 0, 0, 1f);

        float availW = _previewRect.Width - 24;
        float availH = _previewRect.Height - PreviewTopLabelH - PreviewBottomHintH;
        if (availW <= 0f || availH <= 0f) return (0, 0, 0, 0, 1f);

        int maxPins = Math.Max(Recipe.Inputs.Count, Recipe.Outputs.Count);
        float widthFit = availW / Recipe.Width;
        float heightFit = availH / Recipe.Height;

        // Превью не должно превращать 30 пинов в 5 пикселей: шаг между
        // соседними пинами остаётся читаемым, а лишнее уходит в прокрутку.
        float spacingFit = maxPins > 1
            ? MinPreviewPinStep * (maxPins + 1) / Recipe.Height
            : 1f;

        // И никогда больше 1:1 — иначе «размер» в окне не совпадает с чипом на холсте.
        float scale = MathF.Min(widthFit, MathF.Max(heightFit, spacingFit));
        scale = MathF.Min(scale, 1f);
        if (scale <= 0f) scale = 1f;

        float w = Recipe.Width * scale;
        float h = Recipe.Height * scale;
        float cx = _previewRect.X + (_previewRect.Width - w) / 2f;
        _previewViewTop = _previewRect.Y + PreviewTopLabelH;
        _previewViewH = availH;
        float cy = _previewViewTop + (h <= availH ? (availH - h) / 2f : 0f);

        _previewMaxScroll = Math.Max(0, (int)MathF.Ceiling(h - availH));
        _previewScrollY = Math.Clamp(_previewScrollY, 0, _previewMaxScroll);
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

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), DimBackdrop);

        var glow1 = new RRect(_bounds.X - 3, _bounds.Y - 3, _bounds.Width + 6, _bounds.Height + 6);
        var glow2 = new RRect(_bounds.X - 1, _bounds.Y - 1, _bounds.Width + 2, _bounds.Height + 2);
        Raylib.DrawRectangleLinesEx(glow1, 1f, DialogBorderOuter);
        Raylib.DrawRectangleLinesEx(glow2, 1f, DialogBorderMid);

        Raylib.DrawRectangleRec(_bounds, DialogBg);
        Raylib.DrawRectangleLinesEx(_bounds, 2f, DialogBorder);

        int x = (int)_bounds.X + Pad;
        int y = (int)_bounds.Y + Pad;

        string title = EditingTemplate is null ? "Create Macro" : $"Edit Macro: {EditingTemplate.Name}";
        Raylib.DrawText(title, x, y, 26, TextPrimary);
        Raylib.DrawRectangle(x, y + 32, 64, 2, AccentBlue);

        Raylib.DrawText("MACRO NAME", x, _nameLabelY, 13, SectionLabel);

        var fieldBgColor = _nameError ? FieldBgError : FieldBg;
        Raylib.DrawRectangleRec(_nameRect, fieldBgColor);

        var fieldBorder = _nameError ? FieldBorderError
                        : _editingName ? FieldBorderFocus
                        : FieldBorder;
        Raylib.DrawRectangleLinesEx(_nameRect, (_editingName || _nameError) ? 2f : 1f, fieldBorder);

        string displayName;
        RColor nameColor;
        if (string.IsNullOrEmpty(Name))
        {
            displayName = "Enter macro name...";
            nameColor = TextMuted;
        }
        else
        {
            displayName = Name;
            if (_editingName && ((int)(Raylib.GetTime() * 2) % 2 == 0)) displayName += "_";
            nameColor = TextPrimary;
        }
        Raylib.DrawText(displayName, x + 12, (int)_nameRect.Y + 8, 20, nameColor);

        DrawPreview();

        DrawSectionHeader(_rightPanelX, _colorsSectionY, "COLORS", _rightPanelW);

        Raylib.DrawText("Body", _bodyRowX, _bodyRowY - 20, 14, TextSecondary);
        DrawColorRow(_bodyRowX, _bodyRowY, BodyColor, BodyPalette);

        Raylib.DrawText("Wire", _wireRowX, _wireRowY - 20, 14, TextSecondary);
        DrawColorRow(_wireRowX, _wireRowY, WireColor, WirePalette);

        DrawSectionHeader(_rightPanelX, _sizeSectionY, "SIZE", _rightPanelW);

        var m = Raylib.GetMousePosition();
        DrawStepper("W", _wMinus, _wValue, _wPlus, Recipe.Width, m);
        DrawStepper("H", _hMinus, _hValue, _hPlus, Recipe.Height, m);

        Raylib.DrawText($"INPUTS ({Recipe.Inputs.Count})", x, _inputsLabelY, 13, SectionLabel);
        Raylib.DrawText($"OUTPUTS ({Recipe.Outputs.Count})", _outputsLabelX, _outputsLabelY, 13, SectionLabel);

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

        if (_maxScroll > 0)
        {
            int totalRows = Math.Max(Recipe.Inputs.Count, Recipe.Outputs.Count);
            int shownRows = Math.Max(1, (int)(_listViewport.Height / RowH));
            float trackH = _listViewport.Height;
            float thumbH = Math.Max(24f, trackH * shownRows / totalRows);
            float thumbY = _listViewport.Y + (trackH - thumbH) * (_scrollY / (float)_maxScroll);
            var track = new RRect(_bounds.X + _bounds.Width - 10, _listViewport.Y, 6, trackH);
            Raylib.DrawRectangleRec(track, new RColor(40, 46, 62, 255));
            Raylib.DrawRectangleRec(new RRect(track.X, thumbY, 6, thumbH), new RColor(110, 140, 190, 255));
        }

        DrawButton(_resetRect, "Reset", m, ButtonBg, ButtonBgHover, ButtonBorder, TextPrimary);
        DrawButton(_cancelRect, "Cancel", m, ButtonBg, ButtonBgHover, ButtonBorder, TextPrimary);

        string okLabel = EditingTemplate is null ? "Create" : "Apply";
        bool canConfirm = !string.IsNullOrWhiteSpace(Name);

        if (canConfirm)
        {
            RColor primaryBg = EditingTemplate is null ? AccentGreen : AccentBlue;
            RColor primaryHover = EditingTemplate is null ? AccentGreenHover : AccentBlueHover;
            RColor primaryBorder = EditingTemplate is null
                ? new RColor(140, 220, 150, 255)
                : new RColor(140, 190, 255, 255);
            DrawButton(_createRect, okLabel, m, primaryBg, primaryHover, primaryBorder, RColor.White);
        }
        else
        {
            DrawButtonDisabled(_createRect, okLabel, TextMuted);
        }
    }

    private static void DrawSectionHeader(int x, int y, string text, int width)
    {
        Raylib.DrawText(text, x, y, 13, SectionLabel);
        int tw = Raylib.MeasureText(text, 13);
        Raylib.DrawLine(x + tw + 10, y + 8, x + width, y + 8, SectionLine);
    }

    private static void DrawStepper(string label, RRect minus, RRect value, RRect plus, int val, Vector2 m)
    {
        Raylib.DrawText(label, (int)minus.X, (int)minus.Y - 20, 13, SectionLabel);
        DrawSmallBtn(minus, "-", m);
        DrawSmallBtn(plus, "+", m);

        Raylib.DrawRectangleRec(value, FieldBg);
        Raylib.DrawRectangleLinesEx(value, 1f, FieldBorder);
        string s = val.ToString();
        int tw = Raylib.MeasureText(s, 18);
        Raylib.DrawText(s, (int)(value.X + (value.Width - tw) / 2),
            (int)(value.Y + (value.Height - 18) / 2), 18, TextPrimary);
    }

    private static void DrawSmallBtn(RRect r, string label, Vector2 m)
    {
        bool hover = Raylib.CheckCollisionPointRec(m, r);
        Raylib.DrawRectangleRec(r, hover ? ButtonBgHover : ButtonBg);
        Raylib.DrawRectangleLinesEx(r, 1f, ButtonBorder);
        int tw = Raylib.MeasureText(label, 18);
        Raylib.DrawText(label, (int)(r.X + (r.Width - tw) / 2),
            (int)(r.Y + (r.Height - 18) / 2), 18, TextPrimary);
    }

    private void DrawPreview()
    {
        Raylib.DrawRectangleRec(_previewRect, PreviewBg);
        Raylib.DrawRectangleLinesEx(_previewRect, 1f, SectionLine);

        if (Recipe is null) return;

        string sizeLbl = $"{Recipe.Width} × {Recipe.Height} px";
        int sw = Raylib.MeasureText(sizeLbl, 14);
        Raylib.DrawText(sizeLbl,
            (int)(_previewRect.X + (_previewRect.Width - sw) / 2),
            (int)_previewRect.Y + 6,
            14, TextSecondary);

        var (cx, cy, w, h, scale) = PreviewChipRect();
        float chipTop = cy - _previewScrollY;

        Raylib.BeginScissorMode((int)_previewRect.X, (int)_previewViewTop,
            (int)_previewRect.Width, (int)_previewViewH);

        var body = new RColor(BodyColor.R, BodyColor.G, BodyColor.B, (byte)255);

        Raylib.DrawRectangle((int)cx + 3, (int)chipTop + 3, (int)w, (int)h, new RColor(0, 0, 0, 60));
        Raylib.DrawRectangle((int)cx, (int)chipTop, (int)w, (int)h, body);

        var gridColor = new RColor(255, 255, 255, 28);
        float cellPx = GridCellPx * scale;

        if (cellPx >= 4f)
        {
            for (float gx = cx + cellPx; gx < cx + w - 0.5f; gx += cellPx)
                Raylib.DrawLine((int)gx, (int)chipTop, (int)gx, (int)(chipTop + h), gridColor);
            for (float gy = chipTop + cellPx; gy < chipTop + h - 0.5f; gy += cellPx)
                Raylib.DrawLine((int)cx, (int)gy, (int)(cx + w), (int)gy, gridColor);
        }

        Raylib.DrawRectangleLines((int)cx, (int)chipTop, (int)w, (int)h, RColor.White);

        string label = string.IsNullOrEmpty(Name) ? "?" : Name;
        RColor labelColor = string.IsNullOrEmpty(Name) ? new RColor(200, 200, 200, 140) : RColor.White;
        int tw = Raylib.MeasureText(label, 22);
        Raylib.DrawText(label, (int)(cx + (w - tw) / 2), (int)(chipTop + h / 2 - 11), 22, labelColor);

        float pinR = Layout.VisualPinRadius * scale;
        var defaultColor = new RColor(WireColor.R, WireColor.G, WireColor.B, (byte)255);
        var lblColor = new RColor(200, 230, 255, 220);

        for (int i = 0; i < Recipe.Inputs.Count; i++)
        {
            float py = chipTop + Recipe.InputOffsets[i] * h;
            bool dragged = i == _dragInputIdx;
            var c = Recipe.InputPinColors[i] is SDColor ic
                ? new RColor(ic.R, ic.G, ic.B, (byte)255) : defaultColor;

            int bitWidth = Recipe.Inputs[i].ext.BitWidth;
            bool isBus = bitWidth > 1;

            if (isBus)
            {
                float pinSize = Math.Max(10f, bitWidth * 2f) * scale;
                Raylib.DrawRectangle((int)(cx - pinSize / 2), (int)(py - pinSize / 2), (int)pinSize, (int)pinSize, c);
                Raylib.DrawRectangleLines((int)(cx - pinSize / 2), (int)(py - pinSize / 2), (int)pinSize, (int)pinSize, new RColor(0, 0, 0, 120));
                if (dragged) Raylib.DrawRectangleLines((int)(cx - pinSize / 2 - 3), (int)(py - pinSize / 2 - 3), (int)pinSize + 6, (int)pinSize + 6, RColor.Yellow);
            }
            else
            {
                Raylib.DrawCircle((int)cx, (int)py, pinR, c);
                Raylib.DrawCircleLines((int)cx, (int)py, pinR, new RColor(0, 0, 0, 120));
                if (dragged) Raylib.DrawCircleLines((int)cx, (int)py, pinR + 3, RColor.Yellow);
            }

            Raylib.DrawText(Recipe.InputLabels[i], (int)(cx + (isBus ? pinR * 2 : pinR) + 6), (int)(py - 8), 14, lblColor);
        }
        for (int i = 0; i < Recipe.Outputs.Count; i++)
        {
            float py = chipTop + Recipe.OutputOffsets[i] * h;
            bool dragged = i == _dragOutputIdx;
            var c = Recipe.OutputPinColors[i] is SDColor oc
                ? new RColor(oc.R, oc.G, oc.B, (byte)255) : defaultColor;

            int bitWidth = Recipe.Outputs[i].ext.BitWidth;
            bool isBus = bitWidth > 1;

            if (isBus)
            {
                float pinSize = Math.Max(10f, bitWidth * 2f) * scale;
                Raylib.DrawRectangle((int)(cx + w - pinSize / 2), (int)(py - pinSize / 2), (int)pinSize, (int)pinSize, c);
                Raylib.DrawRectangleLines((int)(cx + w - pinSize / 2), (int)(py - pinSize / 2), (int)pinSize, (int)pinSize, new RColor(0, 0, 0, 120));
                if (dragged) Raylib.DrawRectangleLines((int)(cx + w - pinSize / 2 - 3), (int)(py - pinSize / 2 - 3), (int)pinSize + 6, (int)pinSize + 6, RColor.Yellow);
            }
            else
            {
                Raylib.DrawCircle((int)(cx + w), (int)py, pinR, c);
                Raylib.DrawCircleLines((int)(cx + w), (int)py, pinR, new RColor(0, 0, 0, 120));
                if (dragged) Raylib.DrawCircleLines((int)(cx + w), (int)py, pinR + 3, RColor.Yellow);
            }

            string lbl = Recipe.OutputLabels[i];
            int lw = Raylib.MeasureText(lbl, 14);
            Raylib.DrawText(lbl, (int)(cx + w - (isBus ? pinR * 2 : pinR) - 6 - lw), (int)(py - 8), 14, lblColor);
        }

        Raylib.EndScissorMode();

        if (_previewMaxScroll > 0)
        {
            float trackH = _previewViewH;
            float thumbH = MathF.Max(24f, trackH * _previewViewH / (_previewViewH + _previewMaxScroll));
            float thumbY = _previewViewTop
                + (trackH - thumbH) * (_previewScrollY / (float)_previewMaxScroll);
            var track = new RRect(_previewRect.X + _previewRect.Width - 9,
                _previewViewTop, 6, trackH);
            Raylib.DrawRectangleRec(track, new RColor(40, 46, 62, 255));
            Raylib.DrawRectangleRec(new RRect(track.X, thumbY, 6, thumbH),
                new RColor(110, 140, 190, 255));
        }

        string hint = _previewMaxScroll > 0
            ? "scroll preview \u2022 drag pins along sides"
            : "drag pins along sides to reposition";
        Raylib.DrawText(hint,
            (int)_previewRect.X + 12,
            (int)(_previewRect.Y + _previewRect.Height - 20),
            12, TextMuted);
    }

    private static void DrawColorRow(int x, int y, SDColor selected, SDColor[] palette)
    {
        for (int i = 0; i < palette.Length; i++)
        {
            var c = palette[i];
            var rl = new RColor(c.R, c.G, c.B, c.A);
            var r = new RRect(x + i * (CellSize + CellGap), y, CellSize, CellSize);
            Raylib.DrawRectangleRec(r, rl);

            bool isSelected = selected.R == c.R && selected.G == c.G && selected.B == c.B;

            if (isSelected)
            {
                Raylib.DrawRectangleLinesEx(r, 2f, RColor.White);
                Raylib.DrawRectangleLinesEx(
                    new RRect(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4),
                    1f, new RColor(0, 0, 0, 180));
            }
            else
            {
                Raylib.DrawRectangleLinesEx(r, 1f, new RColor(60, 60, 80, 255));
            }
        }
    }

    private static void DrawPinRow(RRect row, string label, Vector2 m, bool dragged,
                                   float offset, SDColor? pinColor, RRect swatch)
    {
        bool hover = Raylib.CheckCollisionPointRec(m, row);
        var bg = dragged
            ? new RColor(60, 70, 110, 255)
            : hover ? new RColor(46, 52, 70, 255)
                    : new RColor(34, 38, 52, 255);

        Raylib.DrawRectangleRec(row, bg);
        Raylib.DrawRectangleLinesEx(row, dragged ? 2f : 1f,
            dragged ? RColor.Yellow : new RColor(60, 70, 95, 255));

        Raylib.DrawText(label, (int)row.X + 10, (int)(row.Y + (row.Height - 18) / 2), 17, TextPrimary);

        string yStr = $"y:{offset:0.00}";
        int yw = Raylib.MeasureText(yStr, 12);
        Raylib.DrawText(yStr, (int)(row.X + row.Width - yw - SwatchSize - 20),
            (int)(row.Y + (row.Height - 12) / 2), 12, TextMuted);

        bool swatchHover = Raylib.CheckCollisionPointRec(m, swatch);
        if (pinColor is SDColor pc)
        {
            Raylib.DrawRectangleRec(swatch, new RColor(pc.R, pc.G, pc.B, (byte)255));
        }
        else
        {
            Raylib.DrawRectangleRec(swatch, new RColor(24, 26, 36, 255));
            Raylib.DrawLine((int)swatch.X + 3, (int)swatch.Y + (int)swatch.Height - 3,
                            (int)swatch.X + (int)swatch.Width - 3, (int)swatch.Y + 3,
                            new RColor(160, 80, 80, 220));
        }
        Raylib.DrawRectangleLinesEx(swatch, swatchHover ? 2f : 1f,
            swatchHover ? RColor.White : new RColor(90, 100, 130, 255));
    }

    private static void DrawButton(RRect r, string label, Vector2 m,
                                   RColor bg, RColor hoverBg, RColor border, RColor textColor)
    {
        bool hover = Raylib.CheckCollisionPointRec(m, r);
        Raylib.DrawRectangleRec(r, hover ? hoverBg : bg);
        Raylib.DrawRectangleLinesEx(r, 1.5f, border);
        int tw = Raylib.MeasureText(label, 18);
        Raylib.DrawText(label, (int)(r.X + (r.Width - tw) / 2),
            (int)(r.Y + (r.Height - 18) / 2), 18, textColor);
    }

    private static void DrawButtonDisabled(RRect r, string label, RColor textColor)
    {
        Raylib.DrawRectangleRec(r, ButtonDisabledBg);
        Raylib.DrawRectangleLinesEx(r, 1f, ButtonDisabledBorder);
        int tw = Raylib.MeasureText(label, 18);
        Raylib.DrawText(label, (int)(r.X + (r.Width - tw) / 2),
            (int)(r.Y + (r.Height - 18) / 2), 18, textColor);
    }

    public static int NextNumber() => ++_counter;
}