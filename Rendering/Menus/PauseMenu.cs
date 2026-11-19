using Raylib_cs;
using System.Numerics;
using RRect = Raylib_cs.Rectangle;
using RColor = Raylib_cs.Color;

namespace LogicSim.Game.Rendering;

public sealed class PauseMenu
{
    public bool IsOpen { get; private set; }
    public bool ResumeRequested { get; private set; }
    public bool SaveRequested { get; private set; }
    public bool MainMenuRequested { get; private set; }

    private const int BtnW = 240;
    private const int BtnH = 46;
    private const int Gap = 12;

    private static readonly RColor DialogBg = new(22, 24, 32, 240);
    private static readonly RColor DialogBorder = new(110, 140, 190, 255);
    private static readonly RColor TextPrimary = new(240, 244, 252, 255);
    private static readonly RColor BtnBg = new(40, 46, 64, 255);
    private static readonly RColor BtnBgHover = new(60, 70, 95, 255);
    private static readonly RColor BtnGreen = new(80, 190, 100, 255);
    private static readonly RColor BtnGreenHover = new(100, 210, 120, 255);
    private static readonly RColor BtnBlue = new(80, 140, 210, 255);
    private static readonly RColor BtnBlueHover = new(100, 160, 230, 255);

    private RRect _bounds;

    public void Open()
    {
        IsOpen = true;
        ResumeRequested = false;
        SaveRequested = false;
        MainMenuRequested = false;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        int w = BtnW + 80;
        int h = 80 + (BtnH + Gap) * 3 + 40;
        _bounds = new RRect((sw - w) / 2, (sh - h) / 2, w, h);
    }

    public void Close()
    {
        IsOpen = false;
    }

    public void Update()
    {
        if (!IsOpen) return;

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            ResumeRequested = true;
            return;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            var m = Raylib.GetMousePosition();
            int cx = (int)_bounds.X + 40;
            int cy = (int)_bounds.Y + 80;

            if (Raylib.CheckCollisionPointRec(m, new RRect(cx, cy, BtnW, BtnH)))
            {
                ResumeRequested = true;
                return;
            }
            if (Raylib.CheckCollisionPointRec(m, new RRect(cx, cy + BtnH + Gap, BtnW, BtnH)))
            {
                SaveRequested = true;
                return;
            }
            if (Raylib.CheckCollisionPointRec(m, new RRect(cx, cy + (BtnH + Gap) * 2, BtnW, BtnH)))
            {
                MainMenuRequested = true;
                return;
            }
        }
    }

    public void Draw()
    {
        if (!IsOpen) return;

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new RColor(0, 0, 0, 160));

        Raylib.DrawRectangleRec(_bounds, DialogBg);
        Raylib.DrawRectangleLinesEx(_bounds, 2f, DialogBorder);

        string title = "Paused";
        int tw = Raylib.MeasureText(title, 32);
        Raylib.DrawText(title, (int)(_bounds.X + (_bounds.Width - tw) / 2), (int)_bounds.Y + 24, 32, TextPrimary);

        var m = Raylib.GetMousePosition();
        int cx = (int)_bounds.X + 40;
        int cy = (int)_bounds.Y + 80;

        DrawBtn(new RRect(cx, cy, BtnW, BtnH), "Continue", m, BtnGreen, BtnGreenHover);
        DrawBtn(new RRect(cx, cy + BtnH + Gap, BtnW, BtnH), "Save", m, BtnBlue, BtnBlueHover);
        DrawBtn(new RRect(cx, cy + (BtnH + Gap) * 2, BtnW, BtnH), "Exit to Main Menu", m, BtnBg, BtnBgHover);
    }

    private static void DrawBtn(RRect r, string text, Vector2 m, RColor baseCol, RColor hoverCol)
    {
        bool hover = Raylib.CheckCollisionPointRec(m, r);
        Raylib.DrawRectangleRec(r, hover ? hoverCol : baseCol);
        Raylib.DrawRectangleLinesEx(r, 1.5f, new RColor(150, 170, 200, 255));
        int tw = Raylib.MeasureText(text, 20);
        Raylib.DrawText(text, (int)(r.X + (r.Width - tw) / 2), (int)(r.Y + (r.Height - 20) / 2), 20, RColor.White);
    }
}