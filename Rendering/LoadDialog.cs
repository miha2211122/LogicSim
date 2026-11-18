using Raylib_cs;
using RRect = Raylib_cs.Rectangle;
using RColor = Raylib_cs.Color;

namespace LogicSim.Game.Rendering;

public sealed class LoadDialog
{
    public bool IsOpen { get; private set; }
    public bool Confirmed { get; private set; }
    public string? SelectedPath { get; private set; }
    public string? DeleteRequestedPath { get; private set; }

    private string _dir = "";
    private readonly List<string> _files = new();
    private int _hoverIndex = -1;
    private int _hoverDeleteIndex = -1;

    private RRect _bounds;
    private const int Width = 500;
    private const int Pad = 20;
    private const int RowH = 34;
    private const int BtnH = 40;
    private const int MaxRows = 8;

    public void Open(string dir)
    {
        _dir = dir;
        _files.Clear();
        SelectedPath = null;
        DeleteRequestedPath = null;
        Confirmed = false;
        IsOpen = true;

        try
        {
            if (System.IO.Directory.Exists(dir))
            {
                foreach (var f in System.IO.Directory.GetFiles(dir, "*.json"))
                    _files.Add(f);
                _files.Sort(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch { }

        int rows = Math.Min(Math.Max(_files.Count, 1), MaxRows);
        int h = Pad * 2 + 34 + 22 + rows * RowH + 20 + BtnH;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        _bounds = new RRect((sw - Width) / 2, (sh - h) / 2, Width, h);
    }

    public void Close()
    {
        IsOpen = false;
    }

    public void Update()
    {
        if (!IsOpen) return;

        DeleteRequestedPath = null;

        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { Close(); return; }

        var m = Raylib.GetMousePosition();
        bool clicked = Raylib.IsMouseButtonPressed(MouseButton.Left);

        int x = (int)_bounds.X + Pad;
        int y = (int)_bounds.Y + Pad;

        y += 34 + 22;

        _hoverIndex = -1;
        _hoverDeleteIndex = -1;
        int shown = Math.Min(_files.Count, MaxRows);
        for (int i = 0; i < shown; i++)
        {
            var r = new RRect(x, y + i * RowH, Width - 2 * Pad, RowH - 4);
            var delRect = new RRect(r.X + r.Width - 24, r.Y + 4, 20, 20);

            if (Raylib.CheckCollisionPointRec(m, r)) _hoverIndex = i;
            if (Raylib.CheckCollisionPointRec(m, delRect)) _hoverDeleteIndex = i;

            if (clicked)
            {
                if (Raylib.CheckCollisionPointRec(m, delRect))
                {
                    DeleteRequestedPath = _files[i];
                    return;
                }
                if (Raylib.CheckCollisionPointRec(m, r))
                {
                    SelectedPath = _files[i];
                }
            }
        }

        int listEnd = y + Math.Min(Math.Max(_files.Count, 1), MaxRows) * RowH + 20;
        int halfW = (Width - 3 * Pad) / 2;
        var loadRect = new RRect(x, listEnd, halfW, BtnH);
        var cancelRect = new RRect(x + halfW + Pad, listEnd, halfW, BtnH);

        if (clicked)
        {
            if (Raylib.CheckCollisionPointRec(m, loadRect) && SelectedPath is not null)
            {
                Confirmed = true;
                IsOpen = false;
                return;
            }
            if (Raylib.CheckCollisionPointRec(m, cancelRect))
            {
                Close();
                return;
            }
        }
    }

    public void Draw()
    {
        if (!IsOpen) return;

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new RColor((byte)0, (byte)0, (byte)0, (byte)170));

        Raylib.DrawRectangleRec(_bounds, new RColor((byte)30, (byte)32, (byte)44, (byte)255));
        Raylib.DrawRectangleLinesEx(_bounds, 2f, new RColor((byte)120, (byte)140, (byte)180, (byte)255));

        int x = (int)_bounds.X + Pad;
        int y = (int)_bounds.Y + Pad;

        Raylib.DrawText("Load Save", x, y, 24, RColor.White);
        y += 34;

        Raylib.DrawText(_dir, x, y, 12, new RColor((byte)150, (byte)150, (byte)150, (byte)255));
        y += 22;

        if (_files.Count == 0)
        {
            Raylib.DrawText("(no saves found)", x + 8, y + 8, 18, RColor.Gray);
        }
        else
        {
            int shown = Math.Min(_files.Count, MaxRows);
            for (int i = 0; i < shown; i++)
            {
                var r = new RRect(x, y + i * RowH, Width - 2 * Pad, RowH - 4);
                bool selected = SelectedPath == _files[i];
                bool hover = i == _hoverIndex;

                RColor bg = selected ? new RColor((byte)80, (byte)130, (byte)200, (byte)255)
                         : hover ? new RColor((byte)55, (byte)60, (byte)80, (byte)255)
                                    : new RColor((byte)40, (byte)44, (byte)58, (byte)255);

                Raylib.DrawRectangleRec(r, bg);
                Raylib.DrawRectangleLinesEx(r, selected ? 2f : 1f,
                    selected ? RColor.White : new RColor((byte)80, (byte)85, (byte)110, (byte)255));

                string label = System.IO.Path.GetFileNameWithoutExtension(_files[i]);
                Raylib.DrawText(label, x + 8, y + i * RowH + 4, 18, RColor.White);

                // Delete button
                var delRect = new RRect(r.X + r.Width - 24, r.Y + 4, 20, 20);
                bool delHover = i == _hoverDeleteIndex;
                Raylib.DrawRectangleRec(delRect, delHover ? new RColor(220, 80, 80, 255) : new RColor(100, 40, 40, 255));
                Raylib.DrawText("x", (int)delRect.X + 6, (int)delRect.Y + 2, 14, RColor.White);
            }
        }

        int shownCount = Math.Min(Math.Max(_files.Count, 1), MaxRows);
        int listEnd = y + shownCount * RowH + 20;
        int halfW = (Width - 3 * Pad) / 2;
        var loadRect = new RRect(x, listEnd, halfW, BtnH);
        var cancelRect = new RRect(x + halfW + Pad, listEnd, halfW, BtnH);

        var m = Raylib.GetMousePosition();
        bool enabled = SelectedPath is not null;

        bool lHover = Raylib.CheckCollisionPointRec(m, loadRect);
        RColor lBg = enabled
            ? (lHover ? new RColor((byte)90, (byte)170, (byte)90, (byte)255)
                      : new RColor((byte)70, (byte)150, (byte)70, (byte)255))
            : new RColor((byte)50, (byte)50, (byte)60, (byte)255);

        Raylib.DrawRectangleRec(loadRect, lBg);
        Raylib.DrawRectangleLinesEx(loadRect, 1f,
            enabled ? new RColor((byte)140, (byte)220, (byte)140, (byte)255)
                    : new RColor((byte)80, (byte)80, (byte)100, (byte)255));
        int ltw = Raylib.MeasureText("Load", 20);
        Raylib.DrawText("Load", (int)(loadRect.X + (loadRect.Width - ltw) / 2), (int)(loadRect.Y + 10), 20,
            enabled ? RColor.White : RColor.Gray);

        bool cHover = Raylib.CheckCollisionPointRec(m, cancelRect);
        Raylib.DrawRectangleRec(cancelRect, cHover
            ? new RColor((byte)70, (byte)70, (byte)90, (byte)255)
            : new RColor((byte)50, (byte)50, (byte)65, (byte)255));
        Raylib.DrawRectangleLinesEx(cancelRect, 1f, new RColor((byte)90, (byte)90, (byte)110, (byte)255));
        int ctw = Raylib.MeasureText("Cancel", 20);
        Raylib.DrawText("Cancel", (int)(cancelRect.X + (cancelRect.Width - ctw) / 2), (int)(cancelRect.Y + 10), 20, RColor.White);
    }
}