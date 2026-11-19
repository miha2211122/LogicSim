using Raylib_cs;
using RRect = Raylib_cs.Rectangle;
using RColor = Raylib_cs.Color;

namespace LogicSim.Game.Rendering;

/// Маленький диалог для period CLK и задержки DELAY (в тактах).
public sealed class TimingDialog
{
    public bool IsOpen { get; private set; }
    public bool Confirmed { get; private set; }
    public int NewValue { get; private set; } = 8;

    private int _min = 1;
    private int _max = 120;
    private string _title = "Timing";
    private string _unit = "ticks";
    private RRect _bounds;
    private RRect _minusRect, _plusRect;
    private RRect _okRect, _cancelRect;

    public void Open(string title, int current, int min, int max, string unit = "ticks")
    {
        _title = title;
        _unit = unit;
        _min = Math.Clamp(min, 1, 10000);
        _max = Math.Clamp(Math.Max(max, _min), _min, 10000);
        NewValue = Math.Clamp(current, _min, _max);
        IsOpen = true;
        Confirmed = false;

        const int w = 320, h = 170;
        _bounds = new RRect(
            (Raylib.GetScreenWidth() - w) / 2f,
            (Raylib.GetScreenHeight() - h) / 2f, w, h);

        _minusRect = new RRect(_bounds.X + 70, _bounds.Y + 62, 44, 40);
        _plusRect = new RRect(_bounds.X + w - 114, _bounds.Y + 62, 44, 40);
        _okRect = new RRect(_bounds.X + 20, _bounds.Y + 118, 130, 32);
        _cancelRect = new RRect(_bounds.X + w - 150, _bounds.Y + 118, 130, 32);
    }

    public void Update()
    {
        if (!IsOpen) return;
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { IsOpen = false; return; }
        if (Raylib.IsKeyPressed(KeyboardKey.Enter))
        {
            Confirmed = true;
            IsOpen = false;
            return;
        }

        int step = Raylib.IsKeyDown(KeyboardKey.LeftShift)
                || Raylib.IsKeyDown(KeyboardKey.RightShift) ? 10 : 1;

        var m = Raylib.GetMousePosition();
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(m, _minusRect)) NewValue = Math.Max(_min, NewValue - step);
            else if (Raylib.CheckCollisionPointRec(m, _plusRect)) NewValue = Math.Min(_max, NewValue + step);
            else if (Raylib.CheckCollisionPointRec(m, _okRect)) { Confirmed = true; IsOpen = false; }
            else if (Raylib.CheckCollisionPointRec(m, _cancelRect)) IsOpen = false;
        }
    }

    public void Draw()
    {
        if (!IsOpen) return;

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new RColor(0, 0, 0, 170));
        Raylib.DrawRectangleRec(_bounds, new RColor(30, 32, 44, 255));
        Raylib.DrawRectangleLinesEx(_bounds, 2f, new RColor(120, 140, 180, 255));

        int tw = Raylib.MeasureText(_title, 20);
        Raylib.DrawText(_title, (int)(_bounds.X + (_bounds.Width - tw) / 2), (int)_bounds.Y + 14, 20, RColor.White);

        var m = Raylib.GetMousePosition();
        RColor btnBg(bool hover) => hover ? new RColor(80, 100, 150, 255) : new RColor(50, 50, 70, 255);

        Raylib.DrawRectangleRec(_minusRect, btnBg(Raylib.CheckCollisionPointRec(m, _minusRect)));
        Raylib.DrawText("-", (int)_minusRect.X + 17, (int)_minusRect.Y + 8, 24, RColor.White);

        Raylib.DrawRectangleRec(_plusRect, btnBg(Raylib.CheckCollisionPointRec(m, _plusRect)));
        Raylib.DrawText("+", (int)_plusRect.X + 16, (int)_plusRect.Y + 8, 24, RColor.White);

        string val = $"{NewValue} {_unit}";
        int vw = Raylib.MeasureText(val, 22);
        Raylib.DrawText(val, (int)(_bounds.X + (_bounds.Width - vw) / 2), (int)_bounds.Y + 70, 22, RColor.Yellow);

        Raylib.DrawText("Shift = x10", (int)(_bounds.X + (_bounds.Width - Raylib.MeasureText("Shift = x10", 12)) / 2),
            (int)_bounds.Y + 100, 12, new RColor(130, 140, 160, 255));

        Raylib.DrawRectangleRec(_okRect, new RColor(70, 150, 70, 255));
        Raylib.DrawText("OK", (int)_okRect.X + 52, (int)_okRect.Y + 6, 18, RColor.White);

        Raylib.DrawRectangleRec(_cancelRect, new RColor(50, 50, 65, 255));
        Raylib.DrawText("Cancel", (int)_cancelRect.X + 40, (int)_cancelRect.Y + 6, 18, RColor.White);
    }
}
