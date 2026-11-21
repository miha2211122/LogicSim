using LogicSim.Game.Core;
using Raylib_cs;
using System.Numerics;
using RRect = Raylib_cs.Rectangle;
using RColor = Raylib_cs.Color;

namespace LogicSim.Game.Rendering;

/// <summary>Экран выбора уровня обучения и истории прохождений.</summary>
public sealed class TutorialMenu
{
    public TutorialLevel? StartRequested { get; private set; }
    public bool BackRequested { get; private set; }

    private TutorialProgress _progress = new();
    private readonly List<RRect> _rows = new();
    private RRect _backRect;
    private RRect _startRect;
    private RRect _hintRect;
    private int _selected = 1;
    private bool _hintVisible;

    private static readonly RColor Bg = new(18, 18, 24, 255);
    private static readonly RColor Panel = new(28, 32, 44, 255);
    private static readonly RColor RowBg = new(38, 43, 58, 255);
    private static readonly RColor RowHover = new(52, 60, 80, 255);
    private static readonly RColor RowDone = new(38, 82, 56, 255);
    private static readonly RColor RowLocked = new(30, 32, 40, 255);
    private static readonly RColor Border = new(90, 110, 150, 255);
    private static readonly RColor Text = new(238, 242, 250, 255);
    private static readonly RColor Muted = new(160, 176, 200, 255);
    private static readonly RColor Accent = new(80, 170, 100, 255);
    private static readonly RColor Blue = new(80, 130, 210, 255);

    public void Open(TutorialProgress progress, int selectLevel = 1)
    {
        _progress = progress;
        _selected = Math.Clamp(selectLevel, 1, TutorialLevel.All.Count);
        _hintVisible = false;
        StartRequested = null;
        BackRequested = false;
    }

    public void Update()
    {
        StartRequested = null;
        BackRequested = false;

        BuildLayout();
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { BackRequested = true; return; }

        if (Raylib.IsKeyPressed(KeyboardKey.Down)) _selected = Math.Min(10, _selected + 1);
        if (Raylib.IsKeyPressed(KeyboardKey.Up)) _selected = Math.Max(1, _selected - 1);
        if (Raylib.IsKeyPressed(KeyboardKey.Enter)) StartSelected();

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;
        var m = Raylib.GetMousePosition();

        if (Raylib.CheckCollisionPointRec(m, _backRect)) { BackRequested = true; return; }
        if (Raylib.CheckCollisionPointRec(m, _hintRect)) { _hintVisible = !_hintVisible; return; }
        if (Raylib.CheckCollisionPointRec(m, _startRect)) { StartSelected(); return; }

        for (int i = 0; i < _rows.Count; i++)
        {
            if (!Raylib.CheckCollisionPointRec(m, _rows[i])) continue;
            _selected = i + 1;
            _hintVisible = false;
            return;
        }
    }

    private void StartSelected() => StartRequested = TutorialLevel.All[_selected - 1];

    private void BuildLayout()
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        _rows.Clear();
        _backRect = new RRect(sw - 150, 26, 120, 38);

        int listX = 40;
        int listY = 110;
        int listW = Math.Min(560, Math.Max(320, sw / 2 - 60));
        int rowH = Math.Clamp((sh - listY - 100) / 10 - 6, 28, 46);
        for (int i = 0; i < 10; i++)
            _rows.Add(new RRect(listX, listY + i * (rowH + 6), listW, rowH));

        int infoX = listX + listW + 20;
        int infoW = Math.Max(260, sw - infoX - 40);
        int bottom = listY + 10 * (rowH + 6);
        _startRect = new RRect(listX, bottom + 12, 220, 44);
        _hintRect = new RRect(infoX + 16, listY + 238, 140, 34);
    }

    public void Draw()
    {
        Raylib.ClearBackground(Bg);
        BuildLayout();

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        var m = Raylib.GetMousePosition();

        Raylib.DrawText("TUTORIAL", 40, 24, 34, Text);
        Raylib.DrawText($"Levels completed: {_progress.CompletedCount}/10   |   Sandbox: {(_progress.IsUnlocked ? "UNLOCKED" : "LOCKED - finish all 10 levels")}",
            40, 68, 16, _progress.IsUnlocked ? Accent : Muted);

        DrawButton(_backRect, "Back", m, new RColor(60, 70, 95, 255), new RColor(80, 95, 125, 255));

        // Level list
        for (int i = 0; i < _rows.Count; i++)
        {
            var r = _rows[i];
            int level = i + 1;
            bool done = _progress.IsCompleted(level);
            bool selected = level == _selected;
            bool hover = Raylib.CheckCollisionPointRec(m, r);
            RColor bg = selected ? RowHover : done ? RowDone : hover ? RowHover : RowBg;
            Raylib.DrawRectangleRec(r, bg);
            Raylib.DrawRectangleLinesEx(r, selected ? 2f : 1f, selected ? RColor.White : Border);

            string mark = done ? "[x]" : "[ ]";
            string label = $"{mark} {level,2}. {TutorialLevel.All[i].Title}";
            int fs = r.Height <= 32 ? 13 : 16;
            Raylib.DrawText(label, (int)r.X + 10, (int)(r.Y + (r.Height - fs) / 2f), fs, done ? new RColor(170, 240, 180, 255) : Text);
        }

        DrawButton(_startRect, "Start level  >", m, Blue, new RColor(110, 160, 235, 255));
        if (!_progress.IsUnlocked)
            Raylib.DrawText("Complete all 10 levels to unlock New Circuit / Load Circuit.", 40, (int)_startRect.Y + 54, 13, Muted);

        // Detail panel
        int infoX = (int)_hintRect.X - 16;
        int infoW = Math.Max(260, sw - infoX - 40);
        int infoY = 110;
        int infoH = Math.Max(320, sh - infoY - 130);
        Raylib.DrawRectangle(infoX, infoY, infoW, infoH, Panel);
        Raylib.DrawRectangleLinesEx(new RRect(infoX, infoY, infoW, infoH), 1f, Border);

        var levelDef = TutorialLevel.All[_selected - 1];
        Raylib.DrawText($"LEVEL {levelDef.Number}/10", infoX + 16, infoY + 14, 22, Accent);
        Raylib.DrawText(levelDef.Title, infoX + 16, infoY + 46, 20, Text);
        DrawWrapped(levelDef.Task, infoX + 16, infoY + 82, infoW - 32, 16, Muted);

        string parts = string.Join(", ", levelDef.AllowedTools.OrderBy(t => t).Select(ToolName));
        Raylib.DrawText($"Parts limit: {levelDef.MaxParts}", infoX + 16, infoY + 168, 15, Text);
        DrawWrapped("Allowed: " + parts, infoX + 16, infoY + 192, infoW - 32, 14, Muted);

        DrawButton(_hintRect, _hintVisible ? "Hide hint" : "Show hint", m, new RColor(70, 90, 70, 255), new RColor(90, 120, 90, 255));
        if (_hintVisible)
            DrawWrapped(levelDef.Hint, infoX + 16, infoY + 286, infoW - 32, 15, new RColor(240, 220, 150, 255));

        // History panel
        int histY = infoY + infoH + 16;
        if (histY + 60 < sh)
        {
            int histH = sh - histY - 24;
            Raylib.DrawRectangle(infoX, histY, infoW, histH, Panel);
            Raylib.DrawRectangleLinesEx(new RRect(infoX, histY, infoW, histH), 1f, Border);
            Raylib.DrawText("PROGRESS HISTORY", infoX + 16, histY + 10, 18, Text);

            int y = histY + 36;
            var last = _progress.History.Take(7).ToList();
            if (last.Count == 0)
            {
                Raylib.DrawText("No attempts yet.", infoX + 16, y, 14, Muted);
            }
            else
            {
                foreach (var a in last)
                {
                    if (y + 18 > histY + histH - 6) break;
                    string line = $"{a.At}  L{a.Level}  {(a.Success ? "PASSED" : "failed")}  {a.Seconds}s";
                    Raylib.DrawText(line, infoX + 16, y, 13, a.Success ? new RColor(150, 230, 160, 255) : new RColor(230, 140, 140, 255));
                    y += 19;
                }
            }
        }
    }

    private static string ToolName(Palette.Tool tool) => tool switch
    {
        Palette.Tool.In => "IN",
        Palette.Tool.Out => "OUT",
        Palette.Tool.Nand => "NAND",
        Palette.Tool.Merge => "MERGE",
        Palette.Tool.Split => "SPLIT",
        Palette.Tool.Clock => "CLK",
        Palette.Tool.Delay => "DELAY",
        Palette.Tool.TriState => "3-STATE",
        Palette.Tool.PullUp => "PULL UP",
        Palette.Tool.PullDown => "PULL DOWN",
        _ => tool.ToString()
    };

    private static void DrawButton(RRect r, string text, Vector2 mouse, RColor normal, RColor hover)
    {
        bool h = Raylib.CheckCollisionPointRec(mouse, r);
        Raylib.DrawRectangleRec(r, h ? hover : normal);
        Raylib.DrawRectangleLinesEx(r, 1.5f, Border);
        int fs = 16;
        int tw = Raylib.MeasureText(text, fs);
        Raylib.DrawText(text, (int)(r.X + (r.Width - tw) / 2), (int)(r.Y + (r.Height - fs) / 2), fs, Text);
    }

    private static void DrawWrapped(string text, int x, int y, int maxWidth, int fs, RColor color)
    {
        int lineY = y;
        string textSoFar = "";
        foreach (string word in text.Split(' '))
        {
            string trial = string.IsNullOrEmpty(textSoFar) ? word : textSoFar + " " + word;
            if (Raylib.MeasureText(trial, fs) > maxWidth && !string.IsNullOrEmpty(textSoFar))
            {
                Raylib.DrawText(textSoFar, x, lineY, fs, color);
                lineY += fs + 4;
                textSoFar = word;
            }
            else textSoFar = trial;
        }
        if (!string.IsNullOrEmpty(textSoFar)) Raylib.DrawText(textSoFar, x, lineY, fs, color);
    }
}
