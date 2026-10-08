using Raylib_cs;
using RRect = Raylib_cs.Rectangle;

namespace LogicSim.Game.Rendering;

public sealed class SaveDialog
{
    public bool IsOpen { get; private set; }
    public bool Confirmed { get; private set; }

    public string FileName { get; set; } = "";
    public string Directory { get; private set; } = "";
    public string? ErrorMessage { get; private set; }

    private RRect _bounds;
    private bool _editing;

    private const int Width = 500;
    private const int Pad = 20;
    private const int BtnH = 40;

    public void Open(string dir, string defaultName)
    {
        Directory = dir;
        FileName = defaultName;
        IsOpen = true;
        Confirmed = false;
        ErrorMessage = null;
        _editing = true;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        _bounds = new RRect((sw - Width) / 2, (sh - 220) / 2, Width, 220);
    }

    public void Close()
    {
        IsOpen = false;
        _editing = false;
    }

    public void Update()
    {
        if (!IsOpen) return;

        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { Close(); return; }
        if (Raylib.IsKeyPressed(KeyboardKey.Enter)) { Confirm(); return; }

        var m = Raylib.GetMousePosition();
        bool clicked = Raylib.IsMouseButtonPressed(MouseButton.Left);

        int x = (int)_bounds.X + Pad;
        int y = (int)_bounds.Y + Pad;

        y += 34;

        var fieldRect = new RRect(x, y, Width - 2 * Pad, 34);
        if (clicked)
            _editing = Raylib.CheckCollisionPointRec(m, fieldRect);
        y += 50;

        y += 22;
        y += 14;

        int halfW = (Width - 3 * Pad) / 2;
        var okRect = new RRect(x, y, halfW, BtnH);
        var cancelRect = new RRect(x + halfW + Pad, y, halfW, BtnH);

        if (clicked)
        {
            if (Raylib.CheckCollisionPointRec(m, okRect)) { Confirm(); return; }
            if (Raylib.CheckCollisionPointRec(m, cancelRect)) { Close(); return; }
        }

        if (_editing)
        {
            int c;
            while ((c = Raylib.GetCharPressed()) != 0)
            {
                char ch = (char)c;
                if (FileName.Length < 40 && IsValid(ch))
                    FileName += ch;
            }
            if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && FileName.Length > 0)
                FileName = FileName.Substring(0, FileName.Length - 1);
        }
    }

    private static bool IsValid(char ch)
    {
        if (char.IsLetterOrDigit(ch)) return true;
        return ch == '_' || ch == '-' || ch == ' ' || ch == '.';
    }

    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(FileName)) { ErrorMessage = "Enter a file name"; return; }
        Confirmed = true;
        IsOpen = false;
    }

    public void Draw()
    {
        if (!IsOpen) return;

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new Color((byte)0, (byte)0, (byte)0, (byte)170));

        Raylib.DrawRectangleRec(_bounds, new Color((byte)30, (byte)32, (byte)44, (byte)255));
        Raylib.DrawRectangleLinesEx(_bounds, 2f, new Color((byte)120, (byte)140, (byte)180, (byte)255));

        int x = (int)_bounds.X + Pad;
        int y = (int)_bounds.Y + Pad;

        Raylib.DrawText("Save As", x, y, 24, Color.White);
        y += 34;

        var fieldRect = new RRect(x, y, Width - 2 * Pad, 34);
        Raylib.DrawRectangleRec(fieldRect, new Color((byte)20, (byte)22, (byte)30, (byte)255));
        Raylib.DrawRectangleLinesEx(fieldRect, _editing ? 2f : 1f,
            _editing ? Color.Yellow : new Color((byte)80, (byte)80, (byte)100, (byte)255));

        string shown = FileName;
        if (_editing && ((int)(Raylib.GetTime() * 2) % 2 == 0)) shown += "_";
        Raylib.DrawText(shown, x + 8, y + 7, 20, Color.White);
        y += 50;

        Raylib.DrawText("Folder:", x, y, 16, Color.LightGray);
        y += 22;
        Raylib.DrawText(Directory, x, y, 14, new Color((byte)180, (byte)180, (byte)180, (byte)255));
        y += 22;

        if (ErrorMessage is { } err)
            Raylib.DrawText(err, x, y, 16, new Color((byte)240, (byte)120, (byte)120, (byte)255));
        y += 14;

        int halfW = (Width - 3 * Pad) / 2;
        var okRect = new RRect(x, y, halfW, BtnH);
        var cancelRect = new RRect(x + halfW + Pad, y, halfW, BtnH);

        var m = Raylib.GetMousePosition();

        bool okHover = Raylib.CheckCollisionPointRec(m, okRect);
        Raylib.DrawRectangleRec(okRect, okHover
            ? new Color((byte)90, (byte)170, (byte)90, (byte)255)
            : new Color((byte)70, (byte)150, (byte)70, (byte)255));
        Raylib.DrawRectangleLinesEx(okRect, 1f, new Color((byte)140, (byte)220, (byte)140, (byte)255));
        int otw = Raylib.MeasureText("Save", 20);
        Raylib.DrawText("Save", (int)(okRect.X + (okRect.Width - otw) / 2), (int)(okRect.Y + 10), 20, Color.White);

        bool cHover = Raylib.CheckCollisionPointRec(m, cancelRect);
        Raylib.DrawRectangleRec(cancelRect, cHover
            ? new Color((byte)70, (byte)70, (byte)90, (byte)255)
            : new Color((byte)50, (byte)50, (byte)65, (byte)255));
        Raylib.DrawRectangleLinesEx(cancelRect, 1f, new Color((byte)90, (byte)90, (byte)110, (byte)255));
        int ctw = Raylib.MeasureText("Cancel", 20);
        Raylib.DrawText("Cancel", (int)(cancelRect.X + (cancelRect.Width - ctw) / 2), (int)(cancelRect.Y + 10), 20, Color.White);
    }
}