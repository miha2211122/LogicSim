using LogicSim.Game.Core;
using Raylib_cs;
using System.Numerics;
using RRect = Raylib_cs.Rectangle;
using RColor = Raylib_cs.Color;

namespace LogicSim.Game.Rendering;

public sealed partial class Editor
{
    private sealed class TutorialRun
    {
        public required TutorialLevel Level;
        public required TutorialProgress Progress;
        public DateTime Started = DateTime.Now;
        public bool HintVisible;
        public TutorialCheck? Result;
    }

    private TutorialRun? _tutorial;

    public bool IsTutorial => _tutorial is not null;
    public bool TutorialAllowsMacros => _tutorial?.Level.AllowMacros == true;
    public TutorialLevel? TutorialLevel => _tutorial?.Level;
    public bool TutorialExitRequested { get; private set; }
    public bool TutorialNextRequested { get; private set; }

    public void StartTutorial(TutorialLevel level, TutorialProgress progress)
    {
        _tutorial = new TutorialRun { Level = level, Progress = progress };
        TutorialExitRequested = false;
        TutorialNextRequested = false;
        _palette.SetAllowedTools(level.AllowedTools, level.AllowMacros);
        _palette.CanMakeMacro = false;
        _palette.ClearSelection();
        _libraryDialog.SetAllowedTools(level.AllowedTools);
        _libraryDialog.Close();
        ShowStatus($"Tutorial {level.Number}/10 - {level.Title}");
    }

    private void EndTutorial()
    {
        _palette.SetAllowedTools(null, true);
        _palette.CanMakeMacro = true;
        _libraryDialog.SetAllowedTools(null);
        _tutorial = null;
    }

    /// <summary>Возвращает true, если событие уже обработано обучением.</summary>
    public bool TutorialHandleInput()
    {
        TutorialExitRequested = false;
        TutorialNextRequested = false;
        if (_tutorial is null) return false;

        var m = Raylib.GetMousePosition();
        var r = TutorialPanelRect();
        var btn = GetTutorialButtonRects(r);

        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { TutorialExitRequested = true; return true; }
        if (Raylib.IsKeyPressed(KeyboardKey.F5)) { CheckTutorial(); return true; }
        if (Raylib.IsKeyPressed(KeyboardKey.H)) { _tutorial.HintVisible = !_tutorial.HintVisible; return true; }
        if (Raylib.IsKeyPressed(KeyboardKey.R)) { RestartTutorial(); return true; }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(m, btn.Check)) { CheckTutorial(); return true; }
            if (Raylib.CheckCollisionPointRec(m, btn.Hint)) { _tutorial.HintVisible = !_tutorial.HintVisible; return true; }
            if (Raylib.CheckCollisionPointRec(m, btn.Restart)) { RestartTutorial(); return true; }
            if (Raylib.CheckCollisionPointRec(m, btn.Exit)) { TutorialExitRequested = true; return true; }
            if (_tutorial.Result is { Success: true })
            {
                if (Raylib.CheckCollisionPointRec(m, btn.Next))
                {
                    if (_tutorial.Level.Number < 10) TutorialNextRequested = true;
                    else TutorialExitRequested = true;
                    return true;
                }
            }
            // Клик по панели не должен ставить деталь на схеме.
            if (Raylib.CheckCollisionPointRec(m, r)) return true;
        }

        return false;
    }

    /// <summary>Ограничение на число деталей уровня.</summary>
    public bool CanPlaceTutorialElements(int count)
    {
        if (_tutorial is null) return true;
        int used = Ctx.Circuit.Elements.Count;
        if (used + count <= _tutorial.Level.MaxParts) return true;
        ShowStatus($"Parts limit: {_tutorial.Level.MaxParts} (used {used}). Remove some parts.");
        return false;
    }

    public void RestartTutorial()
    {
        if (_tutorial is null) return;
        Ctx.Circuit.Clear();
        Ctx.Layout.Clear();
        Ctx.History.Clear();
        _palette.ClearSelection();
        _tutorial.Started = DateTime.Now;
        _tutorial.Result = null;
        ShowStatus("Level restarted.");
    }

    public void CheckTutorial()
    {
        if (_tutorial is null) return;
        var check = _tutorial.Level.Validate(Ctx.Circuit, Ctx.Layout);
        _tutorial.Result = check;
        int seconds = (int)(DateTime.Now - _tutorial.Started).TotalSeconds;
        _tutorial.Progress.Record(_tutorial.Level.Number, check.Success, seconds);
        ShowStatus(check.Message);
    }

    private void ShowStatus(string text)
    {
        StatusMessage = text;
        _statusTimer = 4f;
    }

    private static RRect TutorialPanelRect()
        => new(Raylib.GetScreenWidth() - 390, 124, 374, 320);

    private readonly record struct TutorialButtonRects(RRect Check, RRect Hint, RRect Restart, RRect Exit, RRect Next);

    private static TutorialButtonRects GetTutorialButtonRects(RRect p)
        => new(
            new RRect(p.X + 14, p.Y + 104, 110, 32),
            new RRect(p.X + 132, p.Y + 104, 100, 32),
            new RRect(p.X + 240, p.Y + 104, 110, 32),
            new RRect(p.X + p.Width - 80, p.Y + 8, 70, 26),
            new RRect(p.X + 14, p.Y + 274, 160, 34));

    public void DrawTutorialHud()
    {
        if (_tutorial is null) return;
        var p = TutorialPanelRect();
        var b = GetTutorialButtonRects(p);
        var m = Raylib.GetMousePosition();
        var level = _tutorial.Level;

        Raylib.DrawRectangleRec(p, new RColor(24, 28, 40, 245));
        Raylib.DrawRectangleLinesEx(p, 2f, new RColor(100, 140, 190, 255));

        Raylib.DrawText($"TUTORIAL  {level.Number}/10", (int)p.X + 14, (int)p.Y + 10, 20, new RColor(110, 200, 140, 255));
        DrawButton(b.Exit, "Exit", m, new RColor(70, 70, 90, 255));
        Raylib.DrawText(level.Title, (int)p.X + 14, (int)p.Y + 42, 18, RColor.White);

        int used = Ctx.Circuit.Elements.Count;
        RColor countCol = used > level.MaxParts ? new RColor(240, 110, 110, 255) : RColor.Gray;
        Raylib.DrawText($"Parts: {used}/{level.MaxParts}", (int)p.X + 14, (int)p.Y + 72, 15, countCol);

        DrawButton(b.Check, "Check F5", m, new RColor(60, 120, 80, 255));
        DrawButton(b.Hint, _tutorial.HintVisible ? "Hide H" : "Hint H", m, new RColor(80, 85, 60, 255));
        DrawButton(b.Restart, "Restart R", m, new RColor(80, 70, 70, 255));

        int y = (int)p.Y + 146;
        if (_tutorial.Result is { } res)
        {
            var col = res.Success ? new RColor(140, 230, 150, 255) : new RColor(240, 130, 130, 255);
            DrawWrapped(res.Message, (int)p.X + 14, y, (int)p.Width - 28, 14, col);
            if (res.Success)
            {
                string label = level.Number < 10 ? "Next level  >" : "Finish - unlock sandbox";
                DrawButton(b.Next, label, m, new RColor(70, 140, 90, 255));
            }
        }
        else
        {
            DrawWrapped(level.Task, (int)p.X + 14, y, (int)p.Width - 28, 14, new RColor(200, 215, 235, 255));
        }

        if (_tutorial.Result is { Table.Length: > 0 } withTable && !_tutorial.HintVisible)
        {
            int ty = (int)p.Y + 168;
            foreach (string row in withTable.Table.Split('\n'))
            {
                if (ty > p.Y + p.Height - 20) break;
                Raylib.DrawText(row, (int)p.X + 14, ty, 12, new RColor(190, 210, 235, 255));
                ty += 14;
            }
        }
        else if (_tutorial.HintVisible)
            DrawWrapped("HINT: " + level.Hint, (int)p.X + 14, (int)p.Y + 210, (int)p.Width - 28, 13,
                new RColor(240, 220, 150, 255));
    }

    private static void DrawButton(RRect r, string text, Vector2 mouse, RColor normal)
    {
        bool hover = Raylib.CheckCollisionPointRec(mouse, r);
        Raylib.DrawRectangleRec(r, normal);
        if (hover) Raylib.DrawRectangleLinesEx(r, 2f, RColor.White);
        Raylib.DrawRectangleLinesEx(r, 1f, new RColor(120, 140, 170, 255));
        int fs = 13;
        int tw = Raylib.MeasureText(text, fs);
        Raylib.DrawText(text, (int)(r.X + (r.Width - tw) / 2), (int)(r.Y + (r.Height - fs) / 2), fs, RColor.White);
    }

    private static void DrawWrapped(string text, int x, int y, int maxWidth, int fs, RColor color)
    {
        string line = "";
        int lineY = y;
        foreach (string word in text.Split(' '))
        {
            string trial = line.Length == 0 ? word : line + " " + word;
            if (Raylib.MeasureText(trial, fs) > maxWidth && line.Length > 0)
            {
                Raylib.DrawText(line, x, lineY, fs, color);
                lineY += fs + 3;
                line = word;
            }
            else line = trial;
        }
        if (line.Length > 0) Raylib.DrawText(line, x, lineY, fs, color);
    }
}
