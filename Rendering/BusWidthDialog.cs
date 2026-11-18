using Raylib_cs;
using RRect = Raylib_cs.Rectangle;
using RColor = Raylib_cs.Color;

namespace LogicSim.Game.Rendering;

public sealed class BusWidthDialog
{
    public bool IsOpen { get; private set; }
    public bool Confirmed { get; private set; }
    public int NewWidth { get; private set; } = 8;

    private RRect _bounds;
    private RRect _minusRect, _plusRect;
    private RRect _okRect, _cancelRect;
    private string _title = "";

    public void Open(string title, int currentWidth)
    {
        _title = title;
        NewWidth = currentWidth;
        IsOpen = true;
        Confirmed = false;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        int w = 300, h = 160;
        _bounds = new RRect((sw - w) / 2, (sh - h) / 2, w, h);

        _minusRect = new RRect(_bounds.X + 60, _bounds.Y + 60, 40, 40);
        _plusRect = new RRect(_bounds.X + 200, _bounds.Y + 60, 40, 40);
        _okRect = new RRect(_bounds.X + 20, _bounds.Y + 110, 120, 30);
        _cancelRect = new RRect(_bounds.X + 160, _bounds.Y + 110, 120, 30);
    }

    public void Update()
    {
        if (!IsOpen) return;
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { IsOpen = false; return; }

        var m = Raylib.GetMousePosition();
        bool clicked = Raylib.IsMouseButtonPressed(MouseButton.Left);

        if (clicked)
        {
            if (Raylib.CheckCollisionPointRec(m, _minusRect) && NewWidth > 1) NewWidth--;
            else if (Raylib.CheckCollisionPointRec(m, _plusRect) && NewWidth < 32) NewWidth++;
            else if (Raylib.CheckCollisionPointRec(m, _okRect)) { Confirmed = true; IsOpen = false; }
            else if (Raylib.CheckCollisionPointRec(m, _cancelRect)) IsOpen = false;
        }
    }

    public void Draw()
    {
        if (!IsOpen) return;

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new RColor(0, 0, 0, 170));
        Raylib.DrawRectangleRec(_bounds, new RColor(30, 32, 44, 255));
        Raylib.DrawRectangleLinesEx(_bounds, 2f, new RColor(120, 140, 180, 255));

        int tw = Raylib.MeasureText(_title, 20);
        Raylib.DrawText(_title, (int)(_bounds.X + (_bounds.Width - tw) / 2), (int)_bounds.Y + 15, 20, RColor.White);

        var m = Raylib.GetMousePosition();

        Raylib.DrawRectangleRec(_minusRect, Raylib.CheckCollisionPointRec(m, _minusRect) ? new RColor(80, 100, 150, 255) : new RColor(50, 50, 70, 255));
        Raylib.DrawText("-", (int)_minusRect.X + 15, (int)_minusRect.Y + 10, 20, RColor.White);

        Raylib.DrawRectangleRec(_plusRect, Raylib.CheckCollisionPointRec(m, _plusRect) ? new RColor(80, 100, 150, 255) : new RColor(50, 50, 70, 255));
        Raylib.DrawText("+", (int)_plusRect.X + 15, (int)_plusRect.Y + 10, 20, RColor.White);

        string valStr = NewWidth.ToString();
        int vw = Raylib.MeasureText(valStr, 24);
        Raylib.DrawText(valStr, (int)(_bounds.X + (_bounds.Width - vw) / 2), (int)_bounds.Y + 68, 24, RColor.Yellow);

        Raylib.DrawRectangleRec(_okRect, new RColor(70, 150, 70, 255));
        Raylib.DrawText("OK", (int)_okRect.X + 45, (int)_okRect.Y + 5, 18, RColor.White);

        Raylib.DrawRectangleRec(_cancelRect, new RColor(50, 50, 65, 255));
        Raylib.DrawText("Cancel", (int)_cancelRect.X + 35, (int)_cancelRect.Y + 5, 18, RColor.White);
    }
}