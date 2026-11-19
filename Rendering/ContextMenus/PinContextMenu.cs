using LogicSim.Game.Core;
using Raylib_cs;
using System.Numerics;
using RRect = Raylib_cs.Rectangle;
using RColor = Raylib_cs.Color;
using SDColor = System.Drawing.Color;

namespace LogicSim.Game.Rendering;

public sealed class PinContextMenu
{
    public bool IsOpen { get; private set; }
    public bool RenameRequested { get; private set; }
    public bool PinColorRequested { get; private set; }
    public bool WireColorRequested { get; private set; }
    public SDColor? PickedWireColor { get; private set; }
    public PinHit? Target { get; private set; }

    public SDColor? RecentCustom { get; private set; }

    private static readonly SDColor[] Presets =
    {
        SDColor.FromArgb(80, 220, 80),
        SDColor.FromArgb(220, 80, 80),
        SDColor.FromArgb(80, 140, 220),
        SDColor.FromArgb(220, 200, 80),
        SDColor.FromArgb(200, 80, 200),
        SDColor.FromArgb(80, 220, 220),
        SDColor.FromArgb(220, 140, 60),
        SDColor.FromArgb(200, 200, 200),
    };

    private const int CellSize = 26;
    private const int CellGap = 6;
    private const int Pad = 10;
    private const int RowH = 34;
    private const int SwatchAreaH = CellSize + 16;
    private const int MenuMinW = 200;

    private RRect _bounds;
    private RRect _renameRect;
    private RRect _pinColorRect;
    private RRect _customRect;
    private readonly List<(RRect Rect, SDColor Color)> _swatchRects = new();
    private bool _showWireColors;

    public void Open(PinHit target, Vector2 at, SDColor? recentCustom)
    {
        Target = target;
        RecentCustom = recentCustom;
        IsOpen = true;
        RenameRequested = false;
        PinColorRequested = false;
        WireColorRequested = false;
        PickedWireColor = null;

        // Wire-цвета имеют смысл только для выходных пинов (источник задаёт цвет провода).
        _showWireColors = !target.IsInput;

        _swatchRects.Clear();
        int cols = _showWireColors ? Presets.Length + (recentCustom.HasValue ? 1 : 0) : 0;
        int colorRowW = cols > 0 ? Pad * 2 + cols * CellSize + (cols - 1) * CellGap : 0;
        int menuW = Math.Max(MenuMinW, colorRowW);

        // Строки: Rename, Pin color, (опц.) Wire colors + Custom.
        // Без _showWireColors третьего ряда нет — высота должна это учитывать.
        int h = RowH * 2 + (_showWireColors ? RowH + SwatchAreaH : 0);

        _bounds = new RRect(at.X, at.Y, menuW, h);
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        if (_bounds.X + _bounds.Width > sw) _bounds.X = sw - _bounds.Width - 6;
        if (_bounds.Y + _bounds.Height > sh) _bounds.Y = sh - _bounds.Height - 6;

        int y = (int)_bounds.Y;
        _renameRect = new RRect(_bounds.X, y, menuW, RowH); y += RowH;
        _pinColorRect = new RRect(_bounds.X, y, menuW, RowH); y += RowH;

        if (_showWireColors)
        {
            int sx = (int)_bounds.X + Pad;
            int sy = y + 8;
            for (int i = 0; i < Presets.Length; i++)
                _swatchRects.Add((new RRect(sx + i * (CellSize + CellGap), sy, CellSize, CellSize), Presets[i]));
            if (recentCustom.HasValue)
                _swatchRects.Add((new RRect(sx + Presets.Length * (CellSize + CellGap), sy, CellSize, CellSize), recentCustom.Value));
            y += SwatchAreaH;

            _customRect = new RRect(_bounds.X, y, menuW, RowH);
        }
        else
        {
            _customRect = default;
        }
    }

    public void Close()
    {
        IsOpen = false;
        Target = null;
        RenameRequested = false;
        PinColorRequested = false;
        WireColorRequested = false;
        PickedWireColor = null;
        _showWireColors = false;
        _swatchRects.Clear();
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

        if (clicked)
        {
            if (Raylib.CheckCollisionPointRec(m, _renameRect)) { RenameRequested = true; IsOpen = false; return true; }
            if (Raylib.CheckCollisionPointRec(m, _pinColorRect)) { PinColorRequested = true; IsOpen = false; return true; }

            if (_showWireColors)
            {
                foreach (var (rect, color) in _swatchRects)
                {
                    if (Raylib.CheckCollisionPointRec(m, rect))
                    {
                        PickedWireColor = color;
                        IsOpen = false;
                        return true;
                    }
                }
                if (Raylib.CheckCollisionPointRec(m, _customRect))
                {
                    WireColorRequested = true;
                    IsOpen = false;
                    return true;
                }
            }
        }

        return Raylib.CheckCollisionPointRec(m, _bounds);
    }

    public void Draw()
    {
        if (!IsOpen) return;

        Raylib.DrawRectangleRec(_bounds, new RColor((byte)30, (byte)34, (byte)44, (byte)245));
        Raylib.DrawRectangleLinesEx(_bounds, 1.5f, new RColor((byte)100, (byte)140, (byte)200, (byte)255));

        var m = Raylib.GetMousePosition();
        DrawRowLabel(_renameRect, "Rename pin...", m);
        DrawRowLabel(_pinColorRect, "Pin color...", m);

        if (_showWireColors)
        {
            foreach (var (rect, color) in _swatchRects)
            {
                var rc = new RColor(color.R, color.G, color.B, (byte)255);
                bool hover = Raylib.CheckCollisionPointRec(m, rect);
                Raylib.DrawRectangleRec(rect, rc);
                Raylib.DrawRectangleLinesEx(rect, hover ? 2f : 1f,
                    hover ? RColor.White : new RColor((byte)60, (byte)60, (byte)80, (byte)255));
            }
            Raylib.DrawLine((int)_bounds.X + 4, (int)_customRect.Y,
                            (int)(_bounds.X + _bounds.Width - 4), (int)_customRect.Y,
                            new RColor((byte)60, (byte)70, (byte)100, (byte)255));
            DrawRowLabel(_customRect, "Wire color...", m);
        }
    }

    private static void DrawRowLabel(RRect r, string label, Vector2 m)
    {
        bool hover = Raylib.CheckCollisionPointRec(m, r);
        if (hover) Raylib.DrawRectangleRec(r, new RColor((byte)45, (byte)55, (byte)80, (byte)255));

        var color = hover ? new RColor((byte)180, (byte)220, (byte)255, (byte)255)
                          : new RColor((byte)180, (byte)200, (byte)230, (byte)255);
        Raylib.DrawText(label, (int)r.X + Pad + 8, (int)(r.Y + (r.Height - 18) / 2), 18, color);
    }
}