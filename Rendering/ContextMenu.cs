using System.Numerics;
using LogicSim.Game.Core;
using Raylib_cs;
using SDColor = System.Drawing.Color;
using RRect = Raylib_cs.Rectangle;

namespace LogicSim.Game.Rendering;

public sealed class ContextMenu
{
    private const int CellSize = 28;
    private const int CellGap = 6;
    private const int Pad = 10;
    private const int TitleH = 20;
    private const int BtnH = 32;
    private const int BtnW = 130;

    private static readonly SDColor[] Palette =
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

    public bool IsOpen { get; private set; }
    public bool ViewRequested { get; private set; }
    public bool OpenRequested { get; private set; }
    public bool PinsRequested { get; private set; }

    private Element? _target;
    private RRect _bounds;

    public Element? Target => _target;

    public void Open(Element target, Vector2 at)
    {
        _target = target;
        IsOpen = true;
        ViewRequested = false;
        OpenRequested = false;
        PinsRequested = false;

        int cols = Palette.Length;
        int colorW = Pad * 2 + cols * CellSize + (cols - 1) * CellGap;
        int h = Pad * 2 + TitleH + CellSize + CellGap + TitleH + CellSize + 10 + 3 * (BtnH + 6);

        _bounds = new RRect(at.X, at.Y, colorW, h);

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        if (_bounds.X + _bounds.Width > sw) _bounds.X = sw - _bounds.Width - 6;
        if (_bounds.Y + _bounds.Height > sh) _bounds.Y = sh - _bounds.Height - 6;
    }

    public void Close()
    {
        IsOpen = false;
        _target = null;
        ViewRequested = false;
        OpenRequested = false;
        PinsRequested = false;
    }

    public bool Update()
    {
        if (!IsOpen || _target is null) return false;

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
            if (CellAt(m) is { } h)
            {
                if (h.isBody) _target.BodyColor = Palette[h.index];
                else _target.WireColor = Palette[h.index];
                return true;
            }

            int x = (int)_bounds.X + Pad;
            int y0 = (int)_bounds.Y + Pad + TitleH + CellSize + CellGap + TitleH + CellSize + 10;

            var viewRect = new RRect(x, y0, BtnW, BtnH);
            var openRect = new RRect(x, y0 + BtnH + 6, BtnW, BtnH);
            var pinsRect = new RRect(x, y0 + 2 * (BtnH + 6), BtnW, BtnH);

            if (Raylib.CheckCollisionPointRec(m, viewRect))
            {
                ViewRequested = true;
                IsOpen = false;
                return true;
            }
            if (Raylib.CheckCollisionPointRec(m, openRect))
            {
                OpenRequested = true;
                IsOpen = false;
                return true;
            }
            if (Raylib.CheckCollisionPointRec(m, pinsRect))
            {
                PinsRequested = true;
                IsOpen = false;
                return true;
            }
        }

        return Raylib.CheckCollisionPointRec(m, _bounds);
    }

    private (bool isBody, int index)? CellAt(Vector2 m)
    {
        int x0 = (int)_bounds.X + Pad;
        int y0 = (int)_bounds.Y + Pad;

        int bodyRowY = y0 + TitleH;
        for (int i = 0; i < Palette.Length; i++)
        {
            var r = new RRect(x0 + i * (CellSize + CellGap), bodyRowY, CellSize, CellSize);
            if (Raylib.CheckCollisionPointRec(m, r)) return (true, i);
        }

        int wireRowY = bodyRowY + CellSize + CellGap + TitleH;
        for (int i = 0; i < Palette.Length; i++)
        {
            var r = new RRect(x0 + i * (CellSize + CellGap), wireRowY, CellSize, CellSize);
            if (Raylib.CheckCollisionPointRec(m, r)) return (false, i);
        }

        return null;
    }

    public void Draw()
    {
        if (!IsOpen || _target is null) return;

        Raylib.DrawRectangleRec(_bounds, new Color((byte)30, (byte)32, (byte)44, (byte)245));
        Raylib.DrawRectangleLinesEx(_bounds, 1f, new Color((byte)90, (byte)90, (byte)120, (byte)255));

        int x0 = (int)_bounds.X + Pad;
        int y0 = (int)_bounds.Y + Pad;

        Raylib.DrawText("Body", x0, y0, 18, Color.LightGray);
        int bodyRowY = y0 + TitleH;
        DrawRow(x0, bodyRowY, _target.BodyColor);

        Raylib.DrawText("Wire", x0, bodyRowY + CellSize + CellGap, 18, Color.LightGray);
        int wireRowY = bodyRowY + CellSize + CellGap + TitleH;
        DrawRow(x0, wireRowY, _target.WireColor);

        var m = Raylib.GetMousePosition();
        int by = wireRowY + CellSize + 10;

        DrawActionButton(new RRect(x0, by, BtnW, BtnH), "View", m,
            new Color((byte)80, (byte)140, (byte)200, (byte)255));
        DrawActionButton(new RRect(x0, by + BtnH + 6, BtnW, BtnH), "Open", m,
            new Color((byte)150, (byte)110, (byte)200, (byte)255));
        DrawActionButton(new RRect(x0, by + 2 * (BtnH + 6), BtnW, BtnH), "Pins", m,
            new Color((byte)200, (byte)150, (byte)80, (byte)255));
    }

    private static void DrawActionButton(RRect r, string label, Vector2 m, Color accent)
    {
        bool hover = Raylib.CheckCollisionPointRec(m, r);
        Raylib.DrawRectangleRec(r, hover ? accent : new Color((byte)50, (byte)50, (byte)70, (byte)255));
        Raylib.DrawRectangleLinesEx(r, 1f, new Color((byte)120, (byte)120, (byte)150, (byte)255));
        int tw = Raylib.MeasureText(label, 18);
        Raylib.DrawText(label,
            (int)(r.X + (r.Width - tw) / 2),
            (int)(r.Y + (r.Height - 18) / 2),
            18, Color.White);
    }

    private static void DrawRow(int x0, int y0, SDColor selected)
    {
        for (int i = 0; i < Palette.Length; i++)
        {
            var c = Palette[i];
            var rl = new Color(c.R, c.G, c.B, c.A);
            var r = new RRect(x0 + i * (CellSize + CellGap), y0, CellSize, CellSize);
            Raylib.DrawRectangleRec(r, rl);
            Raylib.DrawRectangleLinesEx(r, selected == c ? 2f : 1f,
                selected == c ? Color.White : new Color((byte)60, (byte)60, (byte)80, (byte)255));
        }
    }
}