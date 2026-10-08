using Raylib_cs;
using RRect = Raylib_cs.Rectangle;

namespace LogicSim.Game.Rendering;

public sealed class Palette
{
    public enum Tool { None, In, Out, Nand }

    public Tool SelectedTool { get; private set; } = Tool.None;
    public ChipTemplate? SelectedTemplate { get; private set; }
    public int PendingCount { get; private set; } = 0;
    public bool HasElements { get; set; }
    public bool CanMakeMacro { get; set; } = true;
    public bool MakeMacroPressed { get; private set; }
    public List<ChipTemplate> Templates { get; } = new();

    public const int BarHeight = 96;
    private const int BtnW = 100;
    private const int BtnH = 60;
    private const int BtnGap = 10;
    private const int Pad = 20;
    private const int MacroW = 180;
    private const int ScrollbarH = 10;

    private readonly int _screenW;
    private readonly int _screenH;

    private readonly List<(RRect Rect, Tool Tool, string Label)> _tools = new();
    private readonly List<(RRect Rect, int Index)> _templateRects = new();
    private RRect _macroRect;
    private RRect _templatesArea;

    private int _scrollX;

    public Palette(int screenW, int screenH)
    {
        _screenW = screenW;
        _screenH = screenH;

        int y = _screenH - BarHeight + (BarHeight - BtnH) / 2;
        int x = Pad;

        AddTool(ref x, y, Tool.In, "IN");
        AddTool(ref x, y, Tool.Out, "OUT");
        AddTool(ref x, y, Tool.Nand, "NAND");

        _macroRect = new RRect(_screenW - Pad - MacroW, y, MacroW, BtnH);

        int templatesLeft = x + BtnGap;
        int templatesRight = (int)_macroRect.X - BtnGap;
        _templatesArea = new RRect(templatesLeft, y, templatesRight - templatesLeft, BtnH);
    }

    private void AddTool(ref int x, int y, Tool tool, string label)
    {
        _tools.Add((new RRect(x, y, BtnW, BtnH), tool, label));
        x += BtnW + BtnGap;
    }

    public bool IsMouseOver() => Raylib.GetMousePosition().Y >= _screenH - BarHeight;

    public void ClearSelection()
    {
        SelectedTool = Tool.None;
        SelectedTemplate = null;
        PendingCount = 0;
    }

    private int MaxScroll()
    {
        if (Templates.Count == 0) return 0;
        int contentW = Templates.Count * (BtnW + BtnGap) - BtnGap;
        return Math.Max(0, contentW - (int)_templatesArea.Width);
    }

    public bool Update()
    {
        MakeMacroPressed = false;
        bool changed = false;

        var m = Raylib.GetMousePosition();

        // Скролл колёсиком над областью шаблонов
        if (Raylib.CheckCollisionPointRec(m, _templatesArea))
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0f)
            {
                int max = MaxScroll();
                if (max > 0)
                {
                    int delta = (int)(-wheel * 60);
                    int newX = Math.Clamp(_scrollX + delta, 0, max);
                    if (newX != _scrollX) { _scrollX = newX; changed = true; }
                }
            }
        }

        if (_scrollX > MaxScroll()) _scrollX = MaxScroll();

        if (Raylib.IsKeyPressed(KeyboardKey.Escape)
            && (SelectedTool != Tool.None || SelectedTemplate != null))
        {
            ClearSelection();
            changed = true;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && IsMouseOver())
        {
            foreach (var (rect, tool, _) in _tools)
            {
                if (Raylib.CheckCollisionPointRec(m, rect))
                {
                    if (SelectedTool == tool && SelectedTemplate is null) PendingCount++;
                    else { SelectedTool = tool; SelectedTemplate = null; PendingCount = 1; }
                    changed = true;
                    break;
                }
            }

            RebuildTemplateRects();
            foreach (var (rect, idx) in _templateRects)
            {
                if (Raylib.CheckCollisionPointRec(m, rect))
                {
                    var tpl = Templates[idx];
                    if (SelectedTemplate == tpl && SelectedTool == Tool.None) PendingCount++;
                    else { SelectedTemplate = tpl; SelectedTool = Tool.None; PendingCount = 1; }
                    changed = true;
                    break;
                }
            }

            if (Raylib.CheckCollisionPointRec(m, _macroRect) && HasElements && CanMakeMacro)
                MakeMacroPressed = true;
        }

        return changed;
    }

    private void RebuildTemplateRects()
    {
        _templateRects.Clear();
        if (Templates.Count == 0) return;

        int baseX = (int)_templatesArea.X - _scrollX;
        int y = (int)_templatesArea.Y;

        for (int i = 0; i < Templates.Count; i++)
        {
            int x = baseX + i * (BtnW + BtnGap);

            // Отсекаем те, что за пределами видимой области
            if (x + BtnW < _templatesArea.X) continue;
            if (x > _templatesArea.X + _templatesArea.Width) break;

            _templateRects.Add((new RRect(x, y, BtnW, BtnH), i));
        }
    }

    public void Draw()
    {
        Raylib.DrawRectangle(0, _screenH - BarHeight, _screenW, BarHeight,
            new Color((byte)20, (byte)20, (byte)26, (byte)255));
        Raylib.DrawLine(0, _screenH - BarHeight, _screenW, _screenH - BarHeight,
            new Color((byte)60, (byte)60, (byte)80, (byte)255));

        var m = Raylib.GetMousePosition();

        // ─── Область шаблонов ───
        // Клиппинг: рисуем внутри области через BeginScissorMode
        Raylib.BeginScissorMode(
            (int)_templatesArea.X, (int)_templatesArea.Y,
            (int)_templatesArea.Width, (int)_templatesArea.Height);

        Raylib.DrawRectangleRec(_templatesArea, new Color((byte)14, (byte)14, (byte)18, (byte)255));

        RebuildTemplateRects();
        foreach (var (rect, idx) in _templateRects)
        {
            var tpl = Templates[idx];
            bool hover = Raylib.CheckCollisionPointRec(m, rect) && Raylib.CheckCollisionPointRec(m, _templatesArea);
            bool sel = SelectedTemplate == tpl;

            Color bg = sel ? new Color((byte)160, (byte)100, (byte)200, (byte)255)
                     : hover ? new Color((byte)70, (byte)60, (byte)90, (byte)255)
                             : new Color(tpl.BodyColor.R, tpl.BodyColor.G, tpl.BodyColor.B, (byte)255);

            Raylib.DrawRectangleRec(rect, bg);
            Raylib.DrawRectangleLinesEx(rect, sel ? 2f : 1f,
                sel ? Color.White : new Color((byte)120, (byte)100, (byte)160, (byte)255));

            string label = tpl.Name;
            int tw = Raylib.MeasureText(label, 18);
            if (tw > rect.Width - 8)
            {
                while (label.Length > 1 && Raylib.MeasureText(label + "...", 18) > rect.Width - 8)
                    label = label.Substring(0, label.Length - 1);
                label += "...";
                tw = Raylib.MeasureText(label, 18);
            }
            Raylib.DrawText(label,
                (int)(rect.X + (rect.Width - tw) / 2),
                (int)(rect.Y + (rect.Height - 18) / 2),
                18, Color.White);

            if (sel && PendingCount > 1) DrawBadge(rect, PendingCount);
        }

        if (Templates.Count == 0)
        {
            Raylib.DrawText("(macros appear here)",
                (int)_templatesArea.X + 10, (int)_templatesArea.Y + 20, 16, Color.Gray);
        }

        Raylib.EndScissorMode();

        // Рамка области
        Raylib.DrawRectangleLinesEx(_templatesArea, 1f,
            new Color((byte)45, (byte)45, (byte)60, (byte)255));

        // ─── Горизонтальный скроллбар ───
        int maxScroll = MaxScroll();
        if (maxScroll > 0)
        {
            int sbY = (int)(_templatesArea.Y + _templatesArea.Height - ScrollbarH - 2);
            int sbX = (int)_templatesArea.X + 2;
            int sbW = (int)_templatesArea.Width - 4;

            Raylib.DrawRectangle(sbX, sbY, sbW, ScrollbarH,
                new Color((byte)30, (byte)30, (byte)40, (byte)255));

            int contentW = Templates.Count * (BtnW + BtnGap) - BtnGap;
            float visibleRatio = (float)_templatesArea.Width / contentW;
            int thumbW = Math.Max(30, (int)(sbW * visibleRatio));
            int thumbX = sbX + (int)((sbW - thumbW) * ((float)_scrollX / maxScroll));

            Raylib.DrawRectangle(thumbX, sbY + 1, thumbW, ScrollbarH - 2,
                new Color((byte)100, (byte)110, (byte)150, (byte)255));
            Raylib.DrawRectangleLines(thumbX, sbY + 1, thumbW, ScrollbarH - 2,
                new Color((byte)150, (byte)160, (byte)200, (byte)255));
        }

        // ─── Инструменты ───
        foreach (var (rect, tool, label) in _tools)
        {
            bool hover = Raylib.CheckCollisionPointRec(m, rect);
            bool sel = SelectedTool == tool && SelectedTemplate is null;
            DrawButton(rect, label, sel, hover,
                new Color((byte)80, (byte)130, (byte)200, (byte)255),
                sel && PendingCount > 1 ? PendingCount.ToString() : null);
        }

        // ─── MAKE MACRO ───
        bool enabled = HasElements && CanMakeMacro;
        bool hovM = Raylib.CheckCollisionPointRec(m, _macroRect);

        Color macroBg = enabled
            ? (hovM ? new Color((byte)100, (byte)180, (byte)100, (byte)255)
                    : new Color((byte)70, (byte)150, (byte)70, (byte)255))
            : new Color((byte)50, (byte)50, (byte)60, (byte)255);

        Raylib.DrawRectangleRec(_macroRect, macroBg);
        Raylib.DrawRectangleLinesEx(_macroRect, 1f,
            enabled ? new Color((byte)150, (byte)230, (byte)150, (byte)255)
                    : new Color((byte)80, (byte)80, (byte)100, (byte)255));

        string ml = "MAKE MACRO";
        int mlw = Raylib.MeasureText(ml, 20);
        Raylib.DrawText(ml,
            (int)(_macroRect.X + (_macroRect.Width - mlw) / 2),
            (int)(_macroRect.Y + (_macroRect.Height - 20) / 2),
            20, enabled ? Color.White : Color.Gray);
    }

    private static void DrawButton(RRect rect, string label, bool selected, bool hover,
        Color accent, string? badge)
    {
        Color bg = selected ? accent
                 : hover ? new Color((byte)60, (byte)60, (byte)80, (byte)255)
                            : new Color((byte)40, (byte)44, (byte)60, (byte)255);

        Raylib.DrawRectangleRec(rect, bg);
        Raylib.DrawRectangleLinesEx(rect, selected ? 2f : 1f,
            selected ? Color.White : new Color((byte)90, (byte)90, (byte)110, (byte)255));

        int tw = Raylib.MeasureText(label, 20);
        Raylib.DrawText(label,
            (int)(rect.X + (rect.Width - tw) / 2),
            (int)(rect.Y + (rect.Height - 20) / 2),
            20, Color.White);

        if (badge is not null) DrawBadge(rect, int.Parse(badge));
    }

    private static void DrawBadge(RRect rect, int count)
    {
        var br = new RRect(rect.X + rect.Width - 22, rect.Y - 6, 26, 22);
        Raylib.DrawRectangleRec(br, new Color((byte)220, (byte)80, (byte)80, (byte)255));
        Raylib.DrawRectangleLinesEx(br, 1f, Color.White);
        string s = "x" + count;
        int tw = Raylib.MeasureText(s, 14);
        Raylib.DrawText(s,
            (int)(br.X + (br.Width - tw) / 2),
            (int)(br.Y + (br.Height - 14) / 2),
            14, Color.White);
    }
}