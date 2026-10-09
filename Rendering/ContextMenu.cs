using LogicSim.Game.Core;
using Raylib_cs;
using System.Numerics;
using RRect = Raylib_cs.Rectangle;
using RColor = Raylib_cs.Color;
using SDColor = System.Drawing.Color;

namespace LogicSim.Game.Rendering;

public sealed class ContextMenu
{
    private const int CellSize = 28;
    private const int CellGap = 6;
    private const int Pad = 10;
    private const int TitleH = 20;
    private const int BtnH = 32;
    private const int BtnW = 130;
    private const int CustomH = 24;
    private const int RowGap = 8;

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
    public bool BodyCustomRequested { get; private set; }

    private Element? _target;
    private RRect _bounds;

    private RRect _bodyCustomRect;
    private RRect _viewRect;
    private RRect _openRect;

    private int _x0;
    private int _bodyRowY;

    private bool _viewEnabled;
    private bool _openEnabled;

    public Element? Target => _target;

    public void Open(Element target, Vector2 at)
    {
        _target = target;
        IsOpen = true;
        ViewRequested = false;
        OpenRequested = false;
        BodyCustomRequested = false;

        bool isChip = target is ChipElement;
        _viewEnabled = isChip;
        _openEnabled = isChip;

        int cols = Palette.Length;
        int colorW = Pad * 2 + cols * CellSize + (cols - 1) * CellGap;
        int h = Pad * 2
              + TitleH + CellSize + CustomH + RowGap
              + 2 * BtnH + 1 * 6;

        _bounds = new RRect(at.X, at.Y, colorW, h);

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        if (_bounds.X + _bounds.Width > sw) _bounds.X = sw - _bounds.Width - 6;
        if (_bounds.Y + _bounds.Height > sh) _bounds.Y = sh - _bounds.Height - 6;

        ComputeLayout();
    }

    private void ComputeLayout()
    {
        _x0 = (int)_bounds.X + Pad;
        int y = (int)_bounds.Y + Pad;
        int innerW = (int)_bounds.Width - Pad * 2;

        y += TitleH;
        _bodyRowY = y;
        y += CellSize;

        _bodyCustomRect = new RRect(_x0, y, innerW, CustomH);
        y += CustomH + RowGap;

        _viewRect = new RRect(_x0, y, BtnW, BtnH);
        _openRect = new RRect(_x0, y + BtnH + 6, BtnW, BtnH);
    }

    public void Close()
    {
        IsOpen = false;
        _target = null;
        ViewRequested = false;
        OpenRequested = false;
        BodyCustomRequested = false;
        _viewEnabled = false;
        _openEnabled = false;
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
                _target.BodyColor = Palette[h];
                return true;
            }

            if (Raylib.CheckCollisionPointRec(m, _bodyCustomRect))
            {
                BodyCustomRequested = true;
                IsOpen = false;
                return true;
            }
            if (_viewEnabled && Raylib.CheckCollisionPointRec(m, _viewRect))
            {
                ViewRequested = true;
                IsOpen = false;
                return true;
            }
            if (_openEnabled && Raylib.CheckCollisionPointRec(m, _openRect))
            {
                OpenRequested = true;
                IsOpen = false;
                return true;
            }
        }

        return Raylib.CheckCollisionPointRec(m, _bounds);
    }

    private int? CellAt(Vector2 m)
    {
        for (int i = 0; i < Palette.Length; i++)
        {
            var r = new RRect(_x0 + i * (CellSize + CellGap), _bodyRowY, CellSize, CellSize);
            if (Raylib.CheckCollisionPointRec(m, r)) return i;
        }
        return null;
    }

    public void Draw()
    {
        if (!IsOpen || _target is null) return;

        Raylib.DrawRectangleRec(_bounds, new RColor((byte)30, (byte)32, (byte)44, (byte)245));
        Raylib.DrawRectangleLinesEx(_bounds, 1f, new RColor((byte)90, (byte)90, (byte)120, (byte)255));

        int y = (int)_bounds.Y + Pad;

        Raylib.DrawText("Body", _x0, y, 18, RColor.LightGray);
        y += TitleH;
        DrawRow(_x0, y, _target.BodyColor);
        y += CellSize;

        DrawCustomButton(_bodyCustomRect, "Custom...");

        var m = Raylib.GetMousePosition();
        DrawActionButton(_viewRect, "View", m,
            new RColor((byte)80, (byte)140, (byte)200, (byte)255), _viewEnabled);
        DrawActionButton(_openRect, "Open", m,
            new RColor((byte)150, (byte)110, (byte)200, (byte)255), _openEnabled);
    }

    private static void DrawCustomButton(RRect r, string label)
    {
        var m = Raylib.GetMousePosition();
        bool hover = Raylib.CheckCollisionPointRec(m, r);
        Raylib.DrawRectangleRec(r, hover
            ? new RColor((byte)90, (byte)100, (byte)140, (byte)255)
            : new RColor((byte)45, (byte)48, (byte)68, (byte)255));
        Raylib.DrawRectangleLinesEx(r, 1f, new RColor((byte)110, (byte)115, (byte)150, (byte)255));

        int tw = Raylib.MeasureText(label, 16);
        Raylib.DrawText(label,
            (int)(r.X + (r.Width - tw) / 2),
            (int)(r.Y + (r.Height - 16) / 2),
            16, RColor.White);
    }

    private static void DrawActionButton(RRect r, string label, Vector2 m, RColor accent, bool enabled)
    {
        if (enabled)
        {
            bool hover = Raylib.CheckCollisionPointRec(m, r);
            Raylib.DrawRectangleRec(r, hover ? accent : new RColor((byte)50, (byte)50, (byte)70, (byte)255));
            Raylib.DrawRectangleLinesEx(r, 1f, new RColor((byte)120, (byte)120, (byte)150, (byte)255));

            int tw = Raylib.MeasureText(label, 18);
            Raylib.DrawText(label,
                (int)(r.X + (r.Width - tw) / 2),
                (int)(r.Y + (r.Height - 18) / 2),
                18, RColor.White);
        }
        else
        {
            Raylib.DrawRectangleRec(r, new RColor((byte)38, (byte)40, (byte)52, (byte)255));
            Raylib.DrawRectangleLinesEx(r, 1f, new RColor((byte)70, (byte)72, (byte)88, (byte)255));

            int tw = Raylib.MeasureText(label, 18);
            Raylib.DrawText(label,
                (int)(r.X + (r.Width - tw) / 2),
                (int)(r.Y + (r.Height - 18) / 2),
                18, new RColor((byte)120, (byte)120, (byte)135, (byte)255));
        }
    }

    private static void DrawRow(int x0, int y0, SDColor selected)
    {
        for (int i = 0; i < Palette.Length; i++)
        {
            var c = Palette[i];
            var rl = new RColor(c.R, c.G, c.B, c.A);
            var r = new RRect(x0 + i * (CellSize + CellGap), y0, CellSize, CellSize);
            Raylib.DrawRectangleRec(r, rl);
            Raylib.DrawRectangleLinesEx(r, selected == c ? 2f : 1f,
                selected == c ? RColor.White : new RColor((byte)60, (byte)60, (byte)80, (byte)255));
        }
    }
}