using System.Numerics;
using LogicSim.Game.Core;
using Raylib_cs;
using RRect = Raylib_cs.Rectangle;
using RColor = Raylib_cs.Color;

namespace LogicSim.Game.Rendering;

public sealed class Palette
{
    public enum Tool { None, In, Out, Nand, Merge, Split, Clock, Delay, TriState, PullUp, PullDown }

    /// <summary>Элемент, который живёт в библиотеке, а не на панели инструментов.</summary>
    public sealed class BuiltinPart
    {
        public required string Name { get; init; }
        public required Tool Tool { get; init; }
        public required string Hint { get; init; }

        /// <summary>Тот же экземпляр, что создаётся при размещении — превью строится по нему.</summary>
        public required Func<Element> Create { get; init; }
    }

    /// <summary>Разделы «PARTS» библиотеки: доступны всегда и не занимают место на панели.</summary>
    public static readonly IReadOnlyList<BuiltinPart> DefaultLibrary = new[]
    {
        new BuiltinPart { Name = "MERGE", Tool = Tool.Merge, Hint = "2-32 bit bus merger", Create = () => CreateElement(Tool.Merge)! },
        new BuiltinPart { Name = "SPLIT", Tool = Tool.Split, Hint = "2-32 bit bus splitter", Create = () => CreateElement(Tool.Split)! },
        new BuiltinPart { Name = "CLK", Tool = Tool.Clock, Hint = "Clock generator", Create = () => CreateElement(Tool.Clock)! },
        new BuiltinPart { Name = "DELAY", Tool = Tool.Delay, Hint = "1-120 tick delay line", Create = () => CreateElement(Tool.Delay)! },
        new BuiltinPart { Name = "3-STATE", Tool = Tool.TriState, Hint = "D passes when E=1, Z when E=0", Create = () => CreateElement(Tool.TriState)! },
        new BuiltinPart { Name = "PULL UP", Tool = Tool.PullUp, Hint = "Weak logic 1 source", Create = () => CreateElement(Tool.PullUp)! },
        new BuiltinPart { Name = "PULL DOWN", Tool = Tool.PullDown, Hint = "Weak logic 0 source", Create = () => CreateElement(Tool.PullDown)! }
    };

    public Tool SelectedTool { get; private set; } = Tool.None;

    /// <summary>Ограничение набора деталей для режима обучения. null — свободная песочница.</summary>
    public IReadOnlySet<Tool>? AllowedTools { get; private set; }
    public bool TemplatesAllowed { get; private set; } = true;
    public string? LastDeniedTool { get; set; }

    public bool IsAllowed(Tool tool) => AllowedTools is null || AllowedTools.Contains(tool);

    public void SetAllowedTools(IEnumerable<Tool>? tools, bool templatesAllowed = true)
    {
        AllowedTools = tools?.ToHashSet();
        TemplatesAllowed = templatesAllowed;
        LastDeniedTool = null;
        if (AllowedTools is not null && SelectedTool != Tool.None && !IsAllowed(SelectedTool))
            ClearSelection();
    }
    public ChipTemplate? SelectedTemplate { get; private set; }
    public int PendingCount { get; private set; } = 0;
    public bool HasElements { get; set; }
    public bool CanMakeMacro { get; set; } = true;
    public bool MakeMacroPressed { get; private set; }
    public bool LibraryButtonPressed { get; private set; }

    public List<ChipTemplate> Templates { get; } = new();
    public List<ChipTemplate> Library { get; } = new();

    public const int BarHeight = 96;
    private const int BtnW = 90;
    private const int BtnH = 60;
    private const int BtnGap = 10;
    private const int ToolBtnW = 64;
    private const int ToolBtnGap = 8;
    private const int Pad = 20;
    private const int MacroW = 180;
    private const int LibraryW = 140;
    private const int ScrollbarH = 10;

    private const int MenuW = 140;
    private const int MenuRowH = 34;
    private const int MenuPad = 4;

    private int _screenW;
    private int _screenH;

    private readonly List<(RRect Rect, Tool Tool, string Label)> _tools = new();
    private readonly List<(RRect Rect, int Index)> _templateRects = new();
    private RRect _macroRect;
    private RRect _libraryRect;
    private RRect _templatesArea;

    private int _scrollX;

    public bool IsContextMenuOpen { get; private set; }
    private ChipTemplate? _ctxTarget;
    private RRect _ctxBounds;
    private RRect _ctxUnstar;

    public Palette(int screenW, int screenH)
    {
        Resize(screenW, screenH);
    }

    /// <summary>Пересчёт геометрии панели при смене размера окна / выходе в fullscreen.</summary>
    public void Resize(int screenW, int screenH)
    {
        _screenW = Math.Max(320, screenW);
        _screenH = Math.Max(BarHeight + 120, screenH);
        CloseContextMenu();
        RebuildLayout();
    }

    private void RebuildLayout()
    {
        _tools.Clear();
        _templateRects.Clear();

        int y = _screenH - BarHeight + (BarHeight - BtnH) / 2;
        int x = Pad;

        AddTool(ref x, y, Tool.In, "IN");
        AddTool(ref x, y, Tool.Out, "OUT");
        AddTool(ref x, y, Tool.Nand, "NAND");

        _macroRect = new RRect(_screenW - Pad - MacroW, y, MacroW, BtnH);
        _libraryRect = new RRect(_screenW - Pad - MacroW - BtnGap - LibraryW, y, LibraryW, BtnH);

        int templatesLeft = x + BtnGap;
        int templatesRight = (int)_libraryRect.X - BtnGap;
        _templatesArea = new RRect(templatesLeft, y, templatesRight - templatesLeft, BtnH);

        _scrollX = Math.Clamp(_scrollX, 0, MaxScroll());
    }

    private void AddTool(ref int x, int y, Tool tool, string label)
    {
        _tools.Add((new RRect(x, y, ToolBtnW, BtnH), tool, label));
        x += ToolBtnW + ToolBtnGap;
    }

    public bool IsMouseOver() => Raylib.GetMousePosition().Y >= _screenH - BarHeight;

    public void ClearSelection()
    {
        SelectedTool = Tool.None;
        SelectedTemplate = null;
        PendingCount = 0;
    }

    /// <summary>Выбор детали из вкладки PARTS библиотеки.</summary>
    /// <summary>Единая фабрика: размещение, голограмма и превью библиотеки используют один и тот же элемент.</summary>
    public static Element? CreateElement(Tool tool) => tool switch
    {
        Tool.In => new InElement(),
        Tool.Out => new OutElement(),
        Tool.Nand => new NandElement(),
        Tool.Merge => new BusMerger(),
        Tool.Split => new BusSplitter(),
        Tool.Clock => new ClockElement(),
        Tool.Delay => new DelayElement(),
        Tool.TriState => new TriStateBufferElement(),
        Tool.PullUp => new PullUpElement(),
        Tool.PullDown => new PullDownElement(),
        _ => null
    };

    public void SelectBuiltin(Tool tool)
    {
        SelectedTool = tool;
        SelectedTemplate = null;
        PendingCount = 1;
    }

    public void CloseContextMenu()
    {
        IsContextMenuOpen = false;
        _ctxTarget = null;
    }

    private int MaxScroll()
    {
        if (Templates.Count == 0) return 0;
        int contentW = Templates.Count * (BtnW + BtnGap) - BtnGap;
        return Math.Max(0, contentW - (int)_templatesArea.Width);
    }

    public bool Update()
    {
        if (IsContextMenuOpen)
        {
            return UpdateContextMenu();
        }

        MakeMacroPressed = false;
        LibraryButtonPressed = false;
        LastDeniedTool = null;
        bool changed = false;

        var m = Raylib.GetMousePosition();

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

        if (TryToolHotkeys()) changed = true;

        if (Raylib.IsMouseButtonPressed(MouseButton.Right) && IsMouseOver())
        {
            RebuildTemplateRects();
            foreach (var (rect, idx) in _templateRects)
            {
                if (idx >= 0 && idx < Templates.Count && Raylib.CheckCollisionPointRec(m, rect))
                {
                    OpenContextMenu(Templates[idx], m);
                    return true;
                }
            }
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && IsMouseOver())
        {
            foreach (var (rect, tool, _) in _tools)
            {
                if (!Raylib.CheckCollisionPointRec(m, rect)) continue;
                if (!IsAllowed(tool)) { LastDeniedTool = tool.ToString(); break; }
                if (SelectedTool == tool && SelectedTemplate is null) PendingCount++;
                else { SelectedTool = tool; SelectedTemplate = null; PendingCount = 1; }
                changed = true;
                break;
            }

            RebuildTemplateRects();
            IEnumerable<(RRect, int)> templateRects = AllowedTools is null || TemplatesAllowed
                ? _templateRects
                : Array.Empty<(RRect, int)>();
            foreach (var (rect, idx) in templateRects)
            {
                if (idx < 0 || idx >= Templates.Count) continue;
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

            if (Raylib.CheckCollisionPointRec(m, _libraryRect))
                LibraryButtonPressed = true;
        }

        return changed;
    }

    private void OpenContextMenu(ChipTemplate target, System.Numerics.Vector2 at)
    {
        _ctxTarget = target;
        IsContextMenuOpen = true;

        int x = (int)at.X;
        int y = (int)at.Y - MenuRowH - MenuPad * 2;
        if (y < 4) y = (int)at.Y + 4;
        if (x + MenuW > _screenW - 4) x = _screenW - MenuW - 4;

        _ctxBounds = new RRect(x, y, MenuW, MenuRowH + MenuPad * 2);
        _ctxUnstar = new RRect(x + MenuPad, y + MenuPad, MenuW - MenuPad * 2, MenuRowH);
    }

    private bool UpdateContextMenu()
    {
        var m = Raylib.GetMousePosition();
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { CloseContextMenu(); return true; }

        bool clicked = Raylib.IsMouseButtonPressed(MouseButton.Left);
        bool rightClicked = Raylib.IsMouseButtonPressed(MouseButton.Right);

        if (clicked || rightClicked)
        {
            if (Raylib.CheckCollisionPointRec(m, _ctxUnstar) && _ctxTarget is not null)
            {
                var tpl = _ctxTarget;
                Templates.Remove(tpl);
                Library.Add(tpl);
                if (SelectedTemplate == tpl) ClearSelection();
                CloseContextMenu();
                return true;
            }

            if (!Raylib.CheckCollisionPointRec(m, _ctxBounds))
            {
                CloseContextMenu();
                return true;
            }
        }

        return true;
    }

    private bool TryToolHotkeys()
    {
        Tool? hotkey = null;
        if (Raylib.IsKeyPressed(KeyboardKey.One)) hotkey = Tool.In;
        else if (Raylib.IsKeyPressed(KeyboardKey.Two)) hotkey = Tool.Out;
        else if (Raylib.IsKeyPressed(KeyboardKey.Three)) hotkey = Tool.Nand;
        else if (Raylib.IsKeyPressed(KeyboardKey.Four)) hotkey = Tool.Merge;
        else if (Raylib.IsKeyPressed(KeyboardKey.Five)) hotkey = Tool.Split;
        else if (Raylib.IsKeyPressed(KeyboardKey.Six)) hotkey = Tool.Clock;
        else if (Raylib.IsKeyPressed(KeyboardKey.Seven)) hotkey = Tool.Delay;

        if (hotkey is { } tool)
        {
            if (!IsAllowed(tool)) { LastDeniedTool = tool.ToString(); return false; }
            if (SelectedTool == tool && SelectedTemplate is null) PendingCount++;
            else { SelectedTool = tool; SelectedTemplate = null; PendingCount = 1; }
            return true;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Zero))
        {
            ClearSelection();
            return true;
        }

        return false;
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
            if (x + BtnW < _templatesArea.X) continue;
            if (x > _templatesArea.X + _templatesArea.Width) break;

            _templateRects.Add((new RRect(x, y, BtnW, BtnH), i));
        }
    }

    public void Draw()
    {
        Raylib.DrawRectangle(0, _screenH - BarHeight, _screenW, BarHeight,
            new RColor((byte)20, (byte)20, (byte)26, (byte)255));
        Raylib.DrawLine(0, _screenH - BarHeight, _screenW, _screenH - BarHeight,
            new RColor((byte)60, (byte)60, (byte)80, (byte)255));

        var m = Raylib.GetMousePosition();

        Raylib.BeginScissorMode(
            (int)_templatesArea.X, (int)_templatesArea.Y,
            (int)_templatesArea.Width, (int)_templatesArea.Height);

        Raylib.DrawRectangleRec(_templatesArea, new RColor((byte)14, (byte)14, (byte)18, (byte)255));

        RebuildTemplateRects();
        foreach (var (rect, idx) in _templateRects)
        {
            if (idx < 0 || idx >= Templates.Count) continue;
            var tpl = Templates[idx];
            bool hover = Raylib.CheckCollisionPointRec(m, rect) && Raylib.CheckCollisionPointRec(m, _templatesArea);
            bool sel = SelectedTemplate == tpl;

            RColor bg = sel ? new RColor((byte)160, (byte)100, (byte)200, (byte)255)
                       : hover ? new RColor((byte)70, (byte)60, (byte)90, (byte)255)
                               : new RColor(tpl.BodyColor.R, tpl.BodyColor.G, tpl.BodyColor.B, (byte)255);

            Raylib.DrawRectangleRec(rect, bg);
            Raylib.DrawRectangleLinesEx(rect, sel ? 2f : 1f,
                sel ? RColor.White : new RColor((byte)120, (byte)100, (byte)160, (byte)255));

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
                18, RColor.White);

            if (sel && PendingCount > 1) DrawBadge(rect, PendingCount);
        }

        if (Templates.Count == 0)
        {
            Raylib.DrawText("(macros appear here, use LIBRARY)",
                (int)_templatesArea.X + 10, (int)_templatesArea.Y + 20, 16, RColor.Gray);
        }

        Raylib.EndScissorMode();

        Raylib.DrawRectangleLinesEx(_templatesArea, 1f, new RColor((byte)45, (byte)45, (byte)60, (byte)255));

        int maxScroll = MaxScroll();
        if (maxScroll > 0)
        {
            int sbY = (int)(_templatesArea.Y + _templatesArea.Height - ScrollbarH - 2);
            int sbX = (int)_templatesArea.X + 2;
            int sbW = (int)_templatesArea.Width - 4;

            Raylib.DrawRectangle(sbX, sbY, sbW, ScrollbarH, new RColor((byte)30, (byte)30, (byte)40, (byte)255));

            int contentW = Templates.Count * (BtnW + BtnGap) - BtnGap;
            float visibleRatio = (float)_templatesArea.Width / contentW;
            int thumbW = Math.Max(30, (int)(sbW * visibleRatio));
            int thumbX = sbX + (int)((sbW - thumbW) * ((float)_scrollX / maxScroll));

            Raylib.DrawRectangle(thumbX, sbY + 1, thumbW, ScrollbarH - 2,
                new RColor((byte)100, (byte)110, (byte)150, (byte)255));
        }

        foreach (var (rect, tool, label) in _tools)
        {
            bool hover = Raylib.CheckCollisionPointRec(m, rect);
            bool sel = SelectedTool == tool && SelectedTemplate is null;
            RColor toolCol = IsAllowed(tool)
                ? new RColor((byte)80, (byte)130, (byte)200, (byte)255)
                : new RColor((byte)48, (byte)50, (byte)58, (byte)255);
            DrawButton(rect, label, sel && IsAllowed(tool), hover && IsAllowed(tool), toolCol,
                sel && PendingCount > 1 ? PendingCount.ToString() : null);
        }

        {
            bool hover = Raylib.CheckCollisionPointRec(m, _libraryRect);
            var bg = hover ? new RColor((byte)120, (byte)90, (byte)180, (byte)255)
                           : new RColor((byte)70, (byte)55, (byte)110, (byte)255);
            Raylib.DrawRectangleRec(_libraryRect, bg);
            Raylib.DrawRectangleLinesEx(_libraryRect, 1f, new RColor((byte)170, (byte)140, (byte)220, (byte)255));
            string txt = $"LIBRARY ({DefaultLibrary.Count + Library.Count})";
            int tw = Raylib.MeasureText(txt, 18);
            Raylib.DrawText(txt,
                (int)(_libraryRect.X + (_libraryRect.Width - tw) / 2),
                (int)(_libraryRect.Y + (_libraryRect.Height - 18) / 2),
                18, RColor.White);
        }

        bool enabled = HasElements && CanMakeMacro;
        bool hovM = Raylib.CheckCollisionPointRec(m, _macroRect);

        RColor macroBg = enabled
            ? (hovM ? new RColor((byte)100, (byte)180, (byte)100, (byte)255)
                    : new RColor((byte)70, (byte)150, (byte)70, (byte)255))
            : new RColor((byte)50, (byte)50, (byte)60, (byte)255);

        Raylib.DrawRectangleRec(_macroRect, macroBg);
        Raylib.DrawRectangleLinesEx(_macroRect, 1f,
            enabled ? new RColor((byte)150, (byte)230, (byte)150, (byte)255)
                    : new RColor((byte)80, (byte)80, (byte)100, (byte)255));

        string ml = "MAKE MACRO";
        int mlw = Raylib.MeasureText(ml, 20);
        Raylib.DrawText(ml,
            (int)(_macroRect.X + (_macroRect.Width - mlw) / 2),
            (int)(_macroRect.Y + (_macroRect.Height - 20) / 2),
            20, enabled ? RColor.White : RColor.Gray);

        if (IsContextMenuOpen)
            DrawContextMenu();
    }

    private void DrawContextMenu()
    {
        Raylib.DrawRectangleRec(_ctxBounds, new RColor((byte)30, (byte)32, (byte)44, (byte)248));
        Raylib.DrawRectangleLinesEx(_ctxBounds, 1.5f, new RColor((byte)120, (byte)140, (byte)180, (byte)255));

        var m = Raylib.GetMousePosition();
        bool hover = Raylib.CheckCollisionPointRec(m, _ctxUnstar);
        if (hover)
            Raylib.DrawRectangleRec(_ctxUnstar, new RColor((byte)60, (byte)70, (byte)100, (byte)255));

        var txtColor = hover ? new RColor((byte)180, (byte)220, (byte)255, (byte)255)
                             : new RColor((byte)200, (byte)210, (byte)230, (byte)255);

        Raylib.DrawText("Unstar", (int)_ctxUnstar.X + 10,
            (int)(_ctxUnstar.Y + (_ctxUnstar.Height - 18) / 2), 18, txtColor);
    }

    private static void DrawButton(RRect rect, string label, bool selected, bool hover,
        RColor accent, string? badge)
    {
        RColor bg = selected ? accent
                 : hover ? new RColor((byte)60, (byte)60, (byte)80, (byte)255)
                            : new RColor((byte)40, (byte)44, (byte)60, (byte)255);

        Raylib.DrawRectangleRec(rect, bg);
        Raylib.DrawRectangleLinesEx(rect, selected ? 2f : 1f,
            selected ? RColor.White : new RColor((byte)90, (byte)90, (byte)110, (byte)255));

        int tw = Raylib.MeasureText(label, 16);
        Raylib.DrawText(label,
            (int)(rect.X + (rect.Width - tw) / 2),
            (int)(rect.Y + (rect.Height - 16) / 2),
            16, RColor.White);

        if (badge is not null) DrawBadge(rect, int.Parse(badge));
    }

    private static void DrawBadge(RRect rect, int count)
    {
        var br = new RRect(rect.X + rect.Width - 22, rect.Y - 6, 26, 22);
        Raylib.DrawRectangleRec(br, new RColor((byte)220, (byte)80, (byte)80, (byte)255));
        Raylib.DrawRectangleLinesEx(br, 1f, RColor.White);
        string s = "x" + count;
        int tw = Raylib.MeasureText(s, 14);
        Raylib.DrawText(s,
            (int)(br.X + (br.Width - tw) / 2),
            (int)(br.Y + (br.Height - 14) / 2),
            14, RColor.White);
    }
}