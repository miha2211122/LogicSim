using Raylib_cs;
using RRect = Raylib_cs.Rectangle;
using RColor = Raylib_cs.Color;

namespace LogicSim.Game.Rendering;

public sealed class RenamePinDialog
{
    public bool IsOpen { get; private set; }
    public bool Confirmed { get; private set; }
    public string NewName { get; private set; } = "";
    public PinHit? Target { get; private set; }
    public string Title { get; private set; } = "";

    private RRect _bounds;
    private bool _editing = true;

    private const int Width = 420;
    private const int Height = 180;
    private const int Pad = 20;
    private const int BtnH = 40;

    public void Open(string title, string current, PinHit target)
    {
        Title = title;
        NewName = current;
        Target = target;
        IsOpen = true;
        Confirmed = false;
        _editing = true;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        _bounds = new RRect((sw - Width) / 2, (sh - Height) / 2, Width, Height);
    }

    public void Close()
    {
        IsOpen = false;
        _editing = false;
        Target = null;
    }

    public void Update()
    {
        if (!IsOpen) return;

        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { Close(); return; }
        if (Raylib.IsKeyPressed(KeyboardKey.Enter)) { Confirmed = true; IsOpen = false; return; }

        var m = Raylib.GetMousePosition();
        bool clicked = Raylib.IsMouseButtonPressed(MouseButton.Left);

        int x = (int)_bounds.X + Pad;
        int y = (int)_bounds.Y + Pad + 34;
        var fieldRect = new RRect(x, y, Width - 2 * Pad, 34);

        if (clicked) _editing = Raylib.CheckCollisionPointRec(m, fieldRect);

        int by = (int)_bounds.Y + Height - Pad - BtnH;
        int halfW = (Width - 3 * Pad) / 2;
        var okRect = new RRect(x, by, halfW, BtnH);
        var cancelRect = new RRect(x + halfW + Pad, by, halfW, BtnH);

        if (clicked)
        {
            if (Raylib.CheckCollisionPointRec(m, okRect)) { Confirmed = true; IsOpen = false; return; }
            if (Raylib.CheckCollisionPointRec(m, cancelRect)) { Close(); return; }
        }

        if (_editing)
        {
            int c;
            while ((c = Raylib.GetCharPressed()) != 0)
            {
                char ch = (char)c;
                if (NewName.Length < 14 && (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'))
                    NewName += ch;
            }
            if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && NewName.Length > 0)
                NewName = NewName.Substring(0, NewName.Length - 1);
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

        Raylib.DrawText(Title, x, y, 20, RColor.White);
        y += 34;

        var fieldRect = new RRect(x, y, Width - 2 * Pad, 34);
        Raylib.DrawRectangleRec(fieldRect, new RColor((byte)20, (byte)22, (byte)30, (byte)255));
        Raylib.DrawRectangleLinesEx(fieldRect, _editing ? 2f : 1f,
            _editing ? RColor.Yellow : new RColor((byte)80, (byte)80, (byte)100, (byte)255));

        string shown = NewName;
        if (_editing && ((int)(Raylib.GetTime() * 2) % 2 == 0)) shown += "_";
        Raylib.DrawText(shown, x + 8, y + 7, 20, RColor.White);

        int by = (int)_bounds.Y + Height - Pad - BtnH;
        int halfW = (Width - 3 * Pad) / 2;
        var okRect = new RRect(x, by, halfW, BtnH);
        var cancelRect = new RRect(x + halfW + Pad, by, halfW, BtnH);

        var m = Raylib.GetMousePosition();

        bool okHover = Raylib.CheckCollisionPointRec(m, okRect);
        Raylib.DrawRectangleRec(okRect, okHover
            ? new RColor((byte)90, (byte)170, (byte)90, (byte)255)
            : new RColor((byte)70, (byte)150, (byte)70, (byte)255));
        Raylib.DrawRectangleLinesEx(okRect, 1f, new RColor((byte)140, (byte)220, (byte)140, (byte)255));
        int otw = Raylib.MeasureText("OK", 20);
        Raylib.DrawText("OK", (int)(okRect.X + (okRect.Width - otw) / 2), (int)(okRect.Y + 10), 20, RColor.White);

        bool cHover = Raylib.CheckCollisionPointRec(m, cancelRect);
        Raylib.DrawRectangleRec(cancelRect, cHover
            ? new RColor((byte)70, (byte)70, (byte)90, (byte)255)
            : new RColor((byte)50, (byte)50, (byte)65, (byte)255));
        Raylib.DrawRectangleLinesEx(cancelRect, 1f, new RColor((byte)90, (byte)90, (byte)110, (byte)255));
        int ctw = Raylib.MeasureText("Cancel", 20);
        Raylib.DrawText("Cancel", (int)(cancelRect.X + (cancelRect.Width - ctw) / 2), (int)(cancelRect.Y + 10), 20, RColor.White);
    }
}