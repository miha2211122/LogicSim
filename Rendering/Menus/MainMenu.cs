using Raylib_cs;
using System.Numerics;
using RRect = Raylib_cs.Rectangle;
using RColor = Raylib_cs.Color;

namespace LogicSim.Game.Rendering;

public sealed class MainMenu
{
    public bool NewGameRequested { get; private set; }
    public bool LoadGameRequested { get; private set; }
    public string? LoadPath { get; private set; }
    public bool ExitRequested { get; private set; }

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

    public MainMenu(string savesDir)
    {
        _savesDir = savesDir;
    }

    public void Update()
    {
        NewGameRequested = false;
        LoadGameRequested = false;
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
            int startY = sh / 2 - 120;
            int cx = sw / 2 - BtnW / 2;

            if (Raylib.CheckCollisionPointRec(m, new RRect(cx, startY, BtnW, BtnH)))
            {
                NewGameRequested = true;
            }
            else if (Raylib.CheckCollisionPointRec(m, new RRect(cx, startY + BtnH + Gap, BtnW, BtnH)))
            {
                _loadDialog.Open(_savesDir);
            }
            else if (Raylib.CheckCollisionPointRec(m, new RRect(cx, startY + (BtnH + Gap) * 2, BtnW, BtnH)))
            {
                OpenUrl("https://github.com/miha2211122/LogicSim");
            }
            else if (Raylib.CheckCollisionPointRec(m, new RRect(cx, startY + (BtnH + Gap) * 3, BtnW, BtnH)))
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
        Raylib.DrawText(title, sw / 2 - tw / 2, sh / 2 - 250, 64, TextPrimary);

        string sub = "Digital Logic Simulator";
        int sw2 = Raylib.MeasureText(sub, 20);
        Raylib.DrawText(sub, sw / 2 - sw2 / 2, sh / 2 - 170, 20, TextSecondary);

        int startY = sh / 2 - 120;
        int cx = sw / 2 - BtnW / 2;
        var m = Raylib.GetMousePosition();

        DrawButton(new RRect(cx, startY, BtnW, BtnH), "New Circuit", m, BtnAccent, BtnAccentHover);
        DrawButton(new RRect(cx, startY + BtnH + Gap, BtnW, BtnH), "Load Circuit", m, BtnBlue, BtnBlueHover);
        DrawButton(new RRect(cx, startY + (BtnH + Gap) * 2, BtnW, BtnH), "Tutorial / GitHub", m, BtnBg, BtnBgHover);
        DrawButton(new RRect(cx, startY + (BtnH + Gap) * 3, BtnW, BtnH), "Exit", m, BtnBg, BtnBgHover);

        Raylib.DrawText("v1.0.0", 20, sh - 30, 16, TextSecondary);

        _loadDialog.Draw();
    }

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