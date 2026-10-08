using LogicSim.Game.Core;
using Raylib_cs;
using RRect = Raylib_cs.Rectangle;

namespace LogicSim.Game.Rendering;

public sealed class PinEditorDialog
{
    public bool IsOpen { get; private set; }
    public bool Confirmed { get; private set; }

    private Element? _target;
    private RRect _bounds;
    private int _focusRow = -1;
    private string _editBuffer = "";

    private const int Width = 420;
    private const int Pad = 16;
    private const int RowH = 32;
    private const int BtnH = 36;

    public void Open(Element target)
    {
        _target = target;
        IsOpen = true;
        Confirmed = false;
        _focusRow = -1;
        _editBuffer = "";

        int rows = target.Inputs.Count + target.Outputs.Count;
        int h = Pad * 2 + 30 + 22 + rows * RowH + 20 + BtnH;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        _bounds = new RRect((sw - Width) / 2, (sh - h) / 2, Width, h);
    }

    public void Close()
    {
        IsOpen = false;
        _target = null;
        _focusRow = -1;
    }

    private void CommitEdit()
    {
        if (_target is null) return;
        int total = _target.Inputs.Count + _target.Outputs.Count;
        if (_focusRow < 0 || _focusRow >= total) return;

        if (_focusRow < _target.Inputs.Count)
            _target.InputLabels[_focusRow] = _editBuffer;
        else
            _target.OutputLabels[_focusRow - _target.Inputs.Count] = _editBuffer;
    }

    private void FocusRow(int row)
    {
        CommitEdit();
        _focusRow = row;
        if (row < 0) { _editBuffer = ""; return; }

        _editBuffer = row < _target!.Inputs.Count
            ? _target.InputLabels[row]
            : _target.OutputLabels[row - _target.Inputs.Count];
    }

    public void Update()
    {
        if (!IsOpen || _target is null) return;

        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { Close(); return; }

        var m = Raylib.GetMousePosition();
        bool clicked = Raylib.IsMouseButtonPressed(MouseButton.Left);

        int x = (int)_bounds.X + Pad;
        int yHeader = (int)_bounds.Y + Pad + 30;

        if (clicked)
        {
            int total = _target.Inputs.Count + _target.Outputs.Count;
            int hitRow = -1;

            int yy = yHeader + 22;
            for (int i = 0; i < _target.Inputs.Count; i++)
            {
                var r = new RRect(x, yy + i * RowH, Width - 2 * Pad, RowH - 4);
                if (Raylib.CheckCollisionPointRec(m, r)) { hitRow = i; break; }
            }
            if (hitRow < 0)
            {
                int yOut = yy + _target.Inputs.Count * RowH + 22;
                for (int i = 0; i < _target.Outputs.Count; i++)
                {
                    var r = new RRect(x, yOut + i * RowH, Width - 2 * Pad, RowH - 4);
                    if (Raylib.CheckCollisionPointRec(m, r)) { hitRow = _target.Inputs.Count + i; break; }
                }
            }

            if (hitRow >= 0)
            {
                FocusRow(hitRow);
            }
            else
            {
                int by = yHeader + 22 + _target.Inputs.Count * RowH + 22 + _target.Outputs.Count * RowH + 20;
                int halfW = (Width - 3 * Pad) / 2;
                var okRect = new RRect(x, by, halfW, BtnH);
                var cancelRect = new RRect(x + halfW + Pad, by, halfW, BtnH);

                if (Raylib.CheckCollisionPointRec(m, okRect))
                {
                    CommitEdit();
                    Confirmed = true;
                    IsOpen = false;
                    return;
                }
                if (Raylib.CheckCollisionPointRec(m, cancelRect))
                {
                    Close();
                    return;
                }
                FocusRow(-1);
            }
        }

        if (_focusRow >= 0)
        {
            int c;
            while ((c = Raylib.GetCharPressed()) != 0)
            {
                char ch = (char)c;
                if (_editBuffer.Length < 6 && (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'))
                    _editBuffer += ch;
            }
            if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && _editBuffer.Length > 0)
                _editBuffer = _editBuffer.Substring(0, _editBuffer.Length - 1);

            if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Tab))
            {
                int total = _target.Inputs.Count + _target.Outputs.Count;
                int next = (_focusRow + 1) % total;
                FocusRow(next);
            }
        }
    }

    public void Draw()
    {
        if (!IsOpen || _target is null) return;

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new Color((byte)0, (byte)0, (byte)0, (byte)160));

        Raylib.DrawRectangleRec(_bounds, new Color((byte)30, (byte)32, (byte)44, (byte)255));
        Raylib.DrawRectangleLinesEx(_bounds, 2f, new Color((byte)120, (byte)140, (byte)180, (byte)255));

        int x = (int)_bounds.X + Pad;
        int y = (int)_bounds.Y + Pad;

        Raylib.DrawText($"Pin labels: {_target.Name}", x, y, 22, Color.White);
        y += 30;

        Raylib.DrawText("Inputs", x, y, 16, Color.LightGray);
        y += 22;
        for (int i = 0; i < _target.Inputs.Count; i++)
        {
            DrawRow(x, y, _target.InputLabels[i], i == _focusRow);
            y += RowH;
        }

        Raylib.DrawText("Outputs", x, y, 16, Color.LightGray);
        y += 22;
        for (int i = 0; i < _target.Outputs.Count; i++)
        {
            int idx = _target.Inputs.Count + i;
            DrawRow(x, y, _target.OutputLabels[i], idx == _focusRow);
            y += RowH;
        }

        y += 20;
        int halfW = (Width - 3 * Pad) / 2;
        var okRect = new RRect(x, y, halfW, BtnH);
        var cancelRect = new RRect(x + halfW + Pad, y, halfW, BtnH);

        var m = Raylib.GetMousePosition();
        bool okHover = Raylib.CheckCollisionPointRec(m, okRect);
        bool cancelHover = Raylib.CheckCollisionPointRec(m, cancelRect);

        Raylib.DrawRectangleRec(okRect, okHover ? new Color((byte)90, (byte)170, (byte)90, (byte)255) : new Color((byte)70, (byte)150, (byte)70, (byte)255));
        Raylib.DrawRectangleLinesEx(okRect, 1f, new Color((byte)140, (byte)220, (byte)140, (byte)255));
        int otw = Raylib.MeasureText("OK", 18);
        Raylib.DrawText("OK", (int)(okRect.X + (okRect.Width - otw) / 2), (int)(okRect.Y + 8), 18, Color.White);

        Raylib.DrawRectangleRec(cancelRect, cancelHover ? new Color((byte)70, (byte)70, (byte)90, (byte)255) : new Color((byte)50, (byte)50, (byte)65, (byte)255));
        Raylib.DrawRectangleLinesEx(cancelRect, 1f, new Color((byte)90, (byte)90, (byte)110, (byte)255));
        int ctw = Raylib.MeasureText("Cancel", 18);
        Raylib.DrawText("Cancel", (int)(cancelRect.X + (cancelRect.Width - ctw) / 2), (int)(cancelRect.Y + 8), 18, Color.White);
    }

    private void DrawRow(int x, int y, string value, bool focused)
    {
        var r = new RRect(x, y, Width - 2 * Pad, RowH - 4);
        Raylib.DrawRectangleRec(r, new Color((byte)20, (byte)22, (byte)30, (byte)255));
        Raylib.DrawRectangleLinesEx(r, focused ? 2f : 1f,
            focused ? Color.Yellow : new Color((byte)80, (byte)80, (byte)100, (byte)255));

        string display = focused ? _editBuffer : value;
        Raylib.DrawText(display, x + 6, y + 6, 18, Color.White);
    }
}