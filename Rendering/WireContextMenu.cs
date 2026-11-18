using Raylib_cs;
using System.Numerics;
using RRect = Raylib_cs.Rectangle;

namespace LogicSim.Game.Rendering;

public sealed class WireContextMenu
{
    public bool IsOpen { get; private set; }
    public bool DeleteRequested { get; private set; }
    public bool LayerRequested { get; private set; }
    public object? Target { get; private set; }

    private RRect _bounds;
    private RRect _layerRect;
    private const int W = 180;
    private const int H = 78;
    private const int Pad = 8;

    public void Open(object target, Vector2 at)
    {
        Target = target;
        IsOpen = true;
        DeleteRequested = false;
        LayerRequested = false;

        _bounds = new RRect(at.X, at.Y, W, H);
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        if (_bounds.X + _bounds.Width > sw) _bounds.X = sw - _bounds.Width - 6;
        if (_bounds.Y + _bounds.Height > sh) _bounds.Y = sh - _bounds.Height - 6;
        _layerRect = new RRect(_bounds.X + Pad, _bounds.Y + H - Pad - 26, W - Pad * 2, 26);
    }

    public void Close()
    {
        IsOpen = false;
        Target = null;
        DeleteRequested = false;
        LayerRequested = false;
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

        if (clicked && Raylib.CheckCollisionPointRec(m, _layerRect))
        {
            LayerRequested = true;
            IsOpen = false;
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

        bool layerHover = Raylib.CheckCollisionPointRec(m, _layerRect);
        Raylib.DrawRectangleRec(_layerRect, layerHover
            ? new Color((byte)60, (byte)100, (byte)90, (byte)255)
            : new Color((byte)35, (byte)60, (byte)55, (byte)255));
        Raylib.DrawRectangleLinesEx(_layerRect, 1f, new Color((byte)100, (byte)190, (byte)170, (byte)255));
        Raylib.DrawText("To Active Layer", (int)_layerRect.X + 10, (int)_layerRect.Y + 4, 16, Color.White);
    }
}