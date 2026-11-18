using LogicSim.Game.Core;
using Raylib_cs;
using System.Numerics;
using RRect = Raylib_cs.Rectangle;
using RColor = Raylib_cs.Color;
using SDColor = System.Drawing.Color;

namespace LogicSim.Game.Rendering;

public sealed class MacroLibraryDialog
{
    public bool IsOpen { get; private set; }

    public ChipTemplate? EditRequested { get; private set; }
    public ChipTemplate? AttachRequested { get; private set; }
    public ChipTemplate? DeleteRequested { get; private set; }

    private List<ChipTemplate>? _templates;
    private List<ChipTemplate>? _library;

    private const int Width = 720;
    private const int Pad = 24;
    private const int RowH = 68;
    private const int BtnH = 40;
    private const int MaxRows = 8;
    private const int SmallBtnW = 96;
    private const int PreviewSize = 48;

    private static readonly RColor DialogBg = new(22, 24, 32, 250);
    private static readonly RColor DialogBorderOuter = new(60, 80, 120, 80);
    private static readonly RColor DialogBorderMid = new(80, 100, 150, 130);
    private static readonly RColor DialogBorder = new(110, 140, 190, 255);
    private static readonly RColor RowBg = new(32, 36, 50, 255);
    private static readonly RColor RowBgHover = new(40, 46, 64, 255);
    private static readonly RColor RowBorder = new(58, 68, 92, 255);
    private static readonly RColor TextPrimary = new(240, 244, 252, 255);
    private static readonly RColor TextSecondary = new(170, 185, 210, 255);
    private static readonly RColor TextMuted = new(110, 125, 150, 255);
    private static readonly RColor SectionLabel = new(150, 175, 215, 255);
    private static readonly RColor DimBackdrop = new(0, 0, 0, 180);
    private static readonly RColor ButtonBg = new(48, 54, 72, 255);
    private static readonly RColor ButtonBgHover = new(62, 70, 92, 255);
    private static readonly RColor ButtonBorder = new(100, 115, 150, 255);
    private static readonly RColor AccentEdit = new(80, 140, 210, 255);
    private static readonly RColor AccentAttach = new(80, 180, 100, 255);
    private static readonly RColor AccentDelete = new(200, 90, 90, 255);

    private RRect _bounds;
    private RRect _closeRect;
    private readonly List<(RRect Row, RRect BtEdit, RRect BtAttach, RRect BtDelete, int Index)> _rows = new();
    private int _headerY;
    private int _subHeaderY;

    public void Open(List<ChipTemplate> templates, List<ChipTemplate> library)
    {
        _templates = templates;
        _library = library;
        IsOpen = true;
        EditRequested = null;
        AttachRequested = null;
        DeleteRequested = null;

        int rows = Math.Min(Math.Max(library.Count, 1), MaxRows);
        int h = Pad * 2 + 30 + 24 + rows * RowH + 20 + BtnH;

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
        int y = (int)_bounds.Y + Pad;

        _headerY = y;
        y += 30;
        _subHeaderY = y;
        y += 24;

        int rowW = Width - Pad * 2;
        int right = x + rowW;
        int delX = right - SmallBtnW;
        int attX = delX - SmallBtnW - 6;
        int editX = attX - SmallBtnW - 6;

        int shown = Math.Min(_library.Count, MaxRows);
        for (int i = 0; i < shown; i++)
        {
            var row = new RRect(x, y + i * RowH, rowW, RowH - 6);
            var btEdit = new RRect(editX, row.Y + (row.Height - 30) / 2, SmallBtnW, 30);
            var btAttach = new RRect(attX, row.Y + (row.Height - 30) / 2, SmallBtnW, 30);
            var btDelete = new RRect(delX, row.Y + (row.Height - 30) / 2, SmallBtnW, 30);
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

        foreach (var (_, btEdit, btAttach, btDelete, idx) in _rows)
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

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), DimBackdrop);

        var glow1 = new RRect(_bounds.X - 3, _bounds.Y - 3, _bounds.Width + 6, _bounds.Height + 6);
        var glow2 = new RRect(_bounds.X - 1, _bounds.Y - 1, _bounds.Width + 2, _bounds.Height + 2);
        Raylib.DrawRectangleLinesEx(glow1, 1f, DialogBorderOuter);
        Raylib.DrawRectangleLinesEx(glow2, 1f, DialogBorderMid);

        Raylib.DrawRectangleRec(_bounds, DialogBg);
        Raylib.DrawRectangleLinesEx(_bounds, 2f, DialogBorder);

        int x = (int)_bounds.X + Pad;

        Raylib.DrawText("Macro Library", x, _headerY, 26, TextPrimary);
        Raylib.DrawRectangle(x, _headerY + 32, 64, 2, AccentEdit);

        Raylib.DrawText($"{_library.Count} macro{(_library.Count == 1 ? "" : "s")} detached from the palette",
            x, _subHeaderY, 14, SectionLabel);

        if (_library.Count == 0)
        {
            Raylib.DrawText("Library is empty.",
                x + 8, _subHeaderY + 30, 18, TextMuted);
            Raylib.DrawText("Right-click a macro in the palette → Unstar to detach it here.",
                x + 8, _subHeaderY + 56, 14, TextMuted);
        }

        var m = Raylib.GetMousePosition();

        foreach (var (row, btEdit, btAttach, btDelete, idx) in _rows)
        {
            if (idx < 0 || idx >= _library.Count) continue;
            var tpl = _library[idx];

            bool rowHover = Raylib.CheckCollisionPointRec(m, row);
            Raylib.DrawRectangleRec(row, rowHover ? RowBgHover : RowBg);
            Raylib.DrawRectangleLinesEx(row, 1f, RowBorder);

            var preview = new RRect(row.X + 10, row.Y + (row.Height - PreviewSize) / 2, PreviewSize, PreviewSize);
            Raylib.DrawRectangleRec(preview, new RColor(tpl.BodyColor.R, tpl.BodyColor.G, tpl.BodyColor.B, (byte)255));
            Raylib.DrawRectangleLinesEx(preview, 1f, new RColor(80, 90, 115, 255));

            int pinY = (int)preview.Y + 8;
            int pinCount = Math.Min(tpl.Prototype.Inputs.Count, 4);
            for (int k = 0; k < pinCount; k++)
            {
                var pinColor = tpl.Prototype.Inputs[k].Color ?? tpl.WireColor;
                Raylib.DrawCircle((int)preview.X, pinY + k * 10, 3,
                    new RColor(pinColor.R, pinColor.G, pinColor.B, (byte)255));
            }
            pinCount = Math.Min(tpl.Prototype.Outputs.Count, 4);
            for (int k = 0; k < pinCount; k++)
            {
                var pinColor = tpl.Prototype.Outputs[k].Color ?? tpl.WireColor;
                Raylib.DrawCircle((int)(preview.X + preview.Width), pinY + k * 10, 3,
                    new RColor(pinColor.R, pinColor.G, pinColor.B, (byte)255));
            }

            int textX = (int)preview.X + (int)preview.Width + 14;
            Raylib.DrawText(tpl.Name, textX, (int)row.Y + 12, 19, TextPrimary);
            Raylib.DrawText($"{tpl.Prototype.Inputs.Count} in • {tpl.Prototype.Outputs.Count} out • {tpl.Prototype.Width}×{tpl.Prototype.Height}",
                textX, (int)row.Y + 36, 13, TextSecondary);

            DrawSmallButton(btEdit, "Edit", m, AccentEdit);
            DrawSmallButton(btAttach, "Attach", m, AccentAttach);
            DrawSmallButton(btDelete, "Delete", m, AccentDelete);
        }

        bool ch = Raylib.CheckCollisionPointRec(m, _closeRect);
        Raylib.DrawRectangleRec(_closeRect, ch ? ButtonBgHover : ButtonBg);
        Raylib.DrawRectangleLinesEx(_closeRect, 1f, ButtonBorder);
        int ctw = Raylib.MeasureText("Close", 18);
        Raylib.DrawText("Close",
            (int)(_closeRect.X + (_closeRect.Width - ctw) / 2),
            (int)(_closeRect.Y + (_closeRect.Height - 18) / 2), 18, TextPrimary);
    }

    private static void DrawSmallButton(RRect r, string label, Vector2 m, RColor accent)
    {
        bool hover = Raylib.CheckCollisionPointRec(m, r);
        Raylib.DrawRectangleRec(r, hover ? accent : ButtonBg);
        Raylib.DrawRectangleLinesEx(r, 1f, hover ? new RColor(180, 200, 240, 255) : ButtonBorder);
        int tw = Raylib.MeasureText(label, 15);
        Raylib.DrawText(label,
            (int)(r.X + (r.Width - tw) / 2),
            (int)(r.Y + (r.Height - 15) / 2), 15, TextPrimary);
    }
}