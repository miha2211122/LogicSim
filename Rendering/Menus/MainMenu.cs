using LogicSim.Game.Core;
using Raylib_cs;
using System.Numerics;
using RRect = Raylib_cs.Rectangle;
using RColor = Raylib_cs.Color;

namespace LogicSim.Game.Rendering;

public sealed class MainMenu
{
    public bool ContinueRequested { get; private set; }
    public bool TutorialRequested { get; private set; }
    public bool NewGameRequested { get; private set; }
    public bool LoadGameRequested { get; private set; }
    public bool ReleasesRequested { get; private set; }
    public string? LoadPath { get; private set; }
    public bool ExitRequested { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void SetError(string? message) => ErrorMessage = message;

    private const int BtnW = 260;
    private const int BtnH = 50;
    private const int Gap = 16;

    private static readonly RColor TextPrimary = new(240, 244, 252, 255);
    private static readonly RColor TextSecondary = new(170, 185, 210, 255);
    private static readonly RColor BtnBg = new(40, 46, 64, 255);
    private static readonly RColor BtnBgHover = new(60, 70, 95, 255);
    private static readonly RColor BtnBorder = new(100, 115, 150, 255);
    private static readonly RColor BtnAccent = new(80, 190, 100, 255);
    private static readonly RColor BtnAccentHover = new(100, 210, 120, 255);
    private static readonly RColor BtnBlue = new(80, 140, 210, 255);
    private static readonly RColor BtnBlueHover = new(100, 160, 230, 255);

    private readonly LoadDialog _loadDialog = new();
    private readonly string _savesDir;
    private bool _hasAutoSave;
    private TutorialProgress? _progress;

    public void SetProgress(TutorialProgress progress) => _progress = progress;
    private bool Unlocked => _progress?.IsUnlocked == true;

    public MainMenu(string savesDir)
    {
        _savesDir = savesDir;
        Refresh();
    }

    /// <summary>Вызывается при возврате в меню: автосохранение могло появиться в этой сессии.</summary>
    public void Refresh() => _hasAutoSave = AutoSave.Exists;

    private int ButtonCount => (_hasAutoSave ? 1 : 0) + 6;
    private int StartY => Raylib.GetScreenHeight() / 2 - (ButtonCount * (BtnH + Gap) - Gap) / 2;

    public void Update()
    {
        // Файл мог появиться или быть удалён извне.
        _hasAutoSave = AutoSave.Exists;

        ContinueRequested = false;
        TutorialRequested = false;
        NewGameRequested = false;
        LoadGameRequested = false;
        ReleasesRequested = false;
        ExitRequested = false;
        LoadPath = null;

        if (_loadDialog.IsOpen)
        {
            _loadDialog.Update();

            if (_loadDialog.DeleteRequestedPath is { } delPath)
            {
                try { System.IO.File.Delete(delPath); } catch { }
                _loadDialog.Open(_savesDir);
                return;
            }

            if (_loadDialog.Confirmed && _loadDialog.SelectedPath is { } p)
            {
                LoadPath = p;
                LoadGameRequested = true;
                _loadDialog.Close();
            }
            return;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            var m = Raylib.GetMousePosition();
            int sw = Raylib.GetScreenWidth();
            int sh = Raylib.GetScreenHeight();
            int startY = StartY;
            int cx = sw / 2 - BtnW / 2;
            // Если автосохранение есть, Все кнопки после Continue сдвинуты на одну позицию.
            int i = _hasAutoSave ? 1 : 0;

            if (_hasAutoSave && Raylib.CheckCollisionPointRec(m, ButtonRect(cx, startY, 0)))
            {
                ContinueRequested = true;
            }

            if (Raylib.CheckCollisionPointRec(m, ButtonRect(cx, startY, i)))
            {
                TutorialRequested = true;
                i++;
            }
            else if (Raylib.CheckCollisionPointRec(m, ButtonRect(cx, startY, i + 1)))
            {
                if (!Unlocked) ErrorMessage = "Finish all 10 tutorial levels to unlock the sandbox.";
                else NewGameRequested = true;
            }
            else if (Raylib.CheckCollisionPointRec(m, ButtonRect(cx, startY, i + 2)))
            {
                if (!Unlocked) ErrorMessage = "Finish all 10 tutorial levels to unlock the sandbox.";
                else _loadDialog.Open(_savesDir);
            }
            else if (Raylib.CheckCollisionPointRec(m, ButtonRect(cx, startY, i + 3)))
            {
                OpenUrl("https://github.com/miha2211122/LogicSim");
            }
            else if (Raylib.CheckCollisionPointRec(m, ButtonRect(cx, startY, i + 4)))
            {
                ReleasesRequested = true;
            }
            else if (Raylib.CheckCollisionPointRec(m, ButtonRect(cx, startY, i + 5)))
            {
                ExitRequested = true;
            }
        }
    }

    public void Draw()
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();

        Raylib.ClearBackground(new RColor(18, 18, 24, 255));

        string title = "LogicSim";
        int tw = Raylib.MeasureText(title, 64);
        Raylib.DrawText(title, sw / 2 - tw / 2, StartY - 112, 64, TextPrimary);

        string sub = "Digital Logic Simulator";
        int sw2 = Raylib.MeasureText(sub, 20);
        Raylib.DrawText(sub, sw / 2 - sw2 / 2, StartY - 36, 20, TextSecondary);

        int startY = StartY;
        int cx = sw / 2 - BtnW / 2;
        var m = Raylib.GetMousePosition();
        int i = 0;

        if (_hasAutoSave)
        {
            string label = "Continue (autosave)";
            if (AutoSave.LastWriteTime is { } t)
                label += $" {t:HH:mm}";
            DrawButton(ButtonRect(cx, startY, i), label, m, BtnAccent, BtnAccentHover);
            i++;
        }

        string tutorialLabel = $"Tutorial  ({_progress?.CompletedCount ?? 0}/10)";
        DrawButton(ButtonRect(cx, startY, i), tutorialLabel, m, BtnBg, BtnBgHover);
        i++;

        var lockedBase = new RColor(45, 48, 58, 255);
        var lockedHover = new RColor(55, 58, 70, 255);
        DrawButton(ButtonRect(cx, startY, i), Unlocked ? "New Circuit" : "New Circuit (LOCKED)", m,
            Unlocked ? BtnBlue : lockedBase, Unlocked ? BtnBlueHover : lockedHover);
        DrawButton(ButtonRect(cx, startY, i + 1), Unlocked ? "Load Circuit" : "Load Circuit (LOCKED)", m,
            Unlocked ? BtnBlue : lockedBase, Unlocked ? BtnBlueHover : lockedHover);
        DrawButton(ButtonRect(cx, startY, i + 2), "GitHub", m, BtnBg, BtnBgHover);
        DrawButton(ButtonRect(cx, startY, i + 3), "Releases", m, BtnBg, BtnBgHover);
        DrawButton(ButtonRect(cx, startY, i + 4), "Exit", m, BtnBg, BtnBgHover);

        if (!Unlocked && ErrorMessage is null)
            Raylib.DrawText("Complete the 10 tutorial levels to unlock the sandbox.",
                sw / 2 - Raylib.MeasureText("Complete the 10 tutorial levels to unlock the sandbox.", 14) / 2,
                (int)ButtonRect(cx, startY, ButtonCount - 1).Y + BtnH + 10, 14, new RColor(230, 190, 110, 255));

        Raylib.DrawText("v1.0.0", 20, sh - 30, 16, TextSecondary);

        if (ErrorMessage is { } err)
        {
            int ew = Raylib.MeasureText(err, 14);
            Raylib.DrawText(err, sw / 2 - ew / 2, (int)ButtonRect(cx, startY, ButtonCount - 1).Y + BtnH + 30, 14,
                new RColor(240, 110, 110, 255));
        }

        _loadDialog.Draw();
    }

    private static RRect ButtonRect(int x, int y, int index) =>
        new(x, y + index * (BtnH + Gap), BtnW, BtnH);

    private static void DrawButton(RRect r, string text, Vector2 m, RColor baseCol, RColor hoverCol)
    {
        bool hover = Raylib.CheckCollisionPointRec(m, r);
        Raylib.DrawRectangleRec(r, hover ? hoverCol : baseCol);
        Raylib.DrawRectangleLinesEx(r, 1.5f, BtnBorder);
        int tw = Raylib.MeasureText(text, 20);
        Raylib.DrawText(text, (int)(r.X + (r.Width - tw) / 2), (int)(r.Y + (r.Height - 20) / 2), 20, RColor.White);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }
}