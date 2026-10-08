using System.Numerics;
using Raylib_cs;
using RRect = Raylib_cs.Rectangle;

namespace LogicSim.Game.Rendering;

public sealed class WireContextMenu
{
    public bool IsOpen { get; private set; }
    public bool DeleteRequested { get; private set; }
    public object? Target { get; private set; }

    private RRect _bounds;
    private const int W = 160;
    private const int H = 40;
    private const int Pad = 8;

    public void Open(object target, Vector2 at)
    {
        Target = target;
        IsOpen = true;
        DeleteRequested = false;

        _bounds = new RRect(at.X, at.Y, W, H);
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        if (_bounds.X + _bounds.Width > sw) _bounds.X = sw - _bounds.Width - 6;
        if (_bounds.Y + _bounds.Height > sh) _bounds.Y = sh - _bounds.Height - 6;
    }

    public void Close()
    {
        IsOpen = false;
        Target = null;
        DeleteRequested = false;
    }

    public bool Update()
    {
        if (!IsOpen) return false;

        var m = Raylib.GetMousePosition();
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { Close(); return true; }

        bool clicked = Raylib.IsMouseButtonPressed(MouseButton.Left);
        bool rightClicked = Raylib.IsMouseButtonPressed(MouseButton.Right);

        if ((clicked || rightClicked) && !Raylib.CheckCollisionPointRec(m, _bounds))
        {
            Close();
            return true;
        }

        if (clicked && Raylib.CheckCollisionPointRec(m, _bounds))
        {
            DeleteRequested = true;
            IsOpen = false;
            return true;
        }

        return Raylib.CheckCollisionPointRec(m, _bounds);
    }

    public void Draw()
    {
        if (!IsOpen) return;

        Raylib.DrawRectangleRec(_bounds, new Color((byte)40, (byte)20, (byte)20, (byte)245));
        Raylib.DrawRectangleLinesEx(_bounds, 1.5f, new Color((byte)180, (byte)80, (byte)80, (byte)255));

        var m = Raylib.GetMousePosition();
        bool hover = Raylib.CheckCollisionPointRec(m, _bounds);
        var color = hover ? new Color((byte)240, (byte)90, (byte)90, (byte)255)
                          : new Color((byte)180, (byte)60, (byte)60, (byte)255);

        Raylib.DrawText("Delete wire", (int)_bounds.X + Pad + 8, (int)_bounds.Y + 10, 18, color);
    }
}