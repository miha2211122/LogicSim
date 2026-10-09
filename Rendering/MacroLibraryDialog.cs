using Raylib_cs;
using System.Numerics;
using RRect = Raylib_cs.Rectangle;
using RColor = Raylib_cs.Color;
using SDColor = System.Drawing.Color;

namespace LogicSim.Game.Rendering;

/// <summary>
/// Окно библиотеки макросов: список откреплённых шаблонов,
/// для каждого — Edit / Attach / Delete.
/// </summary>
public sealed class MacroLibraryDialog
{
    public bool IsOpen { get; private set; }

    public ChipTemplate? EditRequested { get; private set; }
    public ChipTemplate? AttachRequested { get; private set; }
    public ChipTemplate? DeleteRequested { get; private set; }

    private List<ChipTemplate>? _templates;
    private List<ChipTemplate>? _library;

    private const int Width = 660;
    private const int Pad = 20;
    private const int RowH = 56;
    private const int BtnH = 36;
    private const int MaxRows = 9;
    private const int SmallBtnW = 90;

    private RRect _bounds;
    private RRect _closeRect;
    private readonly List<(RRect Row, RRect BtEdit, RRect BtAttach, RRect BtDelete, int Index)> _rows = new();

    public void Open(List<ChipTemplate> templates, List<ChipTemplate> library)
    {
        _templates = templates;
        _library = library;
        IsOpen = true;
        EditRequested = null;
        AttachRequested = null;
        DeleteRequested = null;

        int rows = Math.Min(Math.Max(library.Count, 1), MaxRows);
        int h = Pad * 2 + 30 + 22 + rows * RowH + 20 + BtnH;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        _bounds = new RRect((sw - Width) / 2, (sh - h) / 2, Width, h);

        RebuildRows();
    }

    private void RebuildRows()
    {
        _rows.Clear();
        if (_library is null) return;

        int x = (int)_bounds.X + Pad;
        int y = (int)_bounds.Y + Pad + 30 + 22;

        int rowW = Width - Pad * 2;
        int right = x + rowW;
        int delX = right - SmallBtnW;
        int attX = delX - SmallBtnW - 6;
        int editX = attX - SmallBtnW - 6;

        int shown = Math.Min(_library.Count, MaxRows);
        for (int i = 0; i < shown; i++)
        {
            var row = new RRect(x, y + i * RowH, rowW, RowH - 6);
            var btEdit = new RRect(editX, row.Y + 8, SmallBtnW, RowH - 22);
            var btAttach = new RRect(attX, row.Y + 8, SmallBtnW, RowH - 22);
            var btDelete = new RRect(delX, row.Y + 8, SmallBtnW, RowH - 22);
            _rows.Add((row, btEdit, btAttach, btDelete, i));
        }

        int rowsShown = Math.Min(Math.Max(_library.Count, 1), MaxRows);
        int closeY = y + rowsShown * RowH + 16;
        _closeRect = new RRect((int)_bounds.X + Width - Pad - 140, closeY, 140, BtnH);
    }

    public void Close()
    {
        IsOpen = false;
        _templates = null;
        _library = null;
        _rows.Clear();
        EditRequested = null;
        AttachRequested = null;
        DeleteRequested = null;
    }

    public void Update()
    {
        if (!IsOpen || _library is null) return;

        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { Close(); return; }

        var m = Raylib.GetMousePosition();
        bool clicked = Raylib.IsMouseButtonPressed(MouseButton.Left);

        if (!clicked) return;

        if (Raylib.CheckCollisionPointRec(m, _closeRect))
        {
            Close();
            return;
        }

        foreach (var (row, btEdit, btAttach, btDelete, idx) in _rows)
        {
            if (idx < 0 || idx >= _library.Count) continue;

            if (Raylib.CheckCollisionPointRec(m, btEdit))
            {
                EditRequested = _library[idx];
                return;
            }
            if (Raylib.CheckCollisionPointRec(m, btAttach))
            {
                AttachRequested = _library[idx];
                return;
            }
            if (Raylib.CheckCollisionPointRec(m, btDelete))
            {
                DeleteRequested = _library[idx];
                return;
            }
        }
    }

    public void Draw()
    {
        if (!IsOpen || _library is null) return;

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new RColor((byte)0, (byte)0, (byte)0, (byte)170));
        Raylib.DrawRectangleRec(_bounds, new RColor((byte)30, (byte)32, (byte)44, (byte)255));
        Raylib.DrawRectangleLinesEx(_bounds, 2f, new RColor((byte)120, (byte)140, (byte)180, (byte)255));

        int x = (int)_bounds.X + Pad;
        int y = (int)_bounds.Y + Pad;

        Raylib.DrawText("Macro Library", x, y, 24, RColor.White);
        y += 30;

        Raylib.DrawText($"({_library.Count} detached)", x, y, 14, RColor.LightGray);
        y += 22;

        var m = Raylib.GetMousePosition();

        if (_library.Count == 0)
        {
            Raylib.DrawText("(library is empty)", x + 8, y + 8, 18, RColor.Gray);
        }

        foreach (var (row, btEdit, btAttach, btDelete, idx) in _rows)
        {
            if (idx < 0 || idx >= _library.Count) continue;
            var tpl = _library[idx];

            Raylib.DrawRectangleRec(row, new RColor((byte)40, (byte)44, (byte)58, (byte)255));
            Raylib.DrawRectangleLinesEx(row, 1f, new RColor((byte)80, (byte)85, (byte)110, (byte)255));

            // Цветной квадратик-превью.
            var preview = new RRect(row.X + 8, row.Y + 8, row.Height - 16, row.Height - 16);
            Raylib.DrawRectangleRec(preview, new RColor(tpl.BodyColor.R, tpl.BodyColor.G, tpl.BodyColor.B, (byte)255));
            Raylib.DrawRectangleLinesEx(preview, 1f, RColor.White);

            // Имя.
            Raylib.DrawText(tpl.Name, (int)row.X + 8 + (int)preview.Width + 12,
                (int)(row.Y + (row.Height - 20) / 2), 20, RColor.White);

            // Кнопки.
            DrawSmallButton(btEdit, "Edit", m, new RColor((byte)80, (byte)140, (byte)200, (byte)255));
            DrawSmallButton(btAttach, "Attach", m, new RColor((byte)70, (byte)150, (byte)70, (byte)255));
            DrawSmallButton(btDelete, "Delete", m, new RColor((byte)180, (byte)70, (byte)70, (byte)255));
        }

        // Close.
        bool ch = Raylib.CheckCollisionPointRec(m, _closeRect);
        Raylib.DrawRectangleRec(_closeRect, ch
            ? new RColor((byte)70, (byte)70, (byte)90, (byte)255)
            : new RColor((byte)50, (byte)50, (byte)65, (byte)255));
        Raylib.DrawRectangleLinesEx(_closeRect, 1f, new RColor((byte)90, (byte)90, (byte)110, (byte)255));
        int ctw = Raylib.MeasureText("Close", 20);
        Raylib.DrawText("Close",
            (int)(_closeRect.X + (_closeRect.Width - ctw) / 2),
            (int)(_closeRect.Y + (_closeRect.Height - 20) / 2), 20, RColor.White);
    }

    private static void DrawSmallButton(RRect r, string label, Vector2 m, RColor accent)
    {
        bool hover = Raylib.CheckCollisionPointRec(m, r);
        Raylib.DrawRectangleRec(r, hover ? accent : new RColor((byte)50, (byte)55, (byte)75, (byte)255));
        Raylib.DrawRectangleLinesEx(r, 1f, new RColor((byte)110, (byte)120, (byte)150, (byte)255));
        int tw = Raylib.MeasureText(label, 16);
        Raylib.DrawText(label,
            (int)(r.X + (r.Width - tw) / 2),
            (int)(r.Y + (r.Height - 16) / 2), 16, RColor.White);
    }
}