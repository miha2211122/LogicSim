using Raylib_cs;
using RRect = Raylib_cs.Rectangle;
using RColor = Raylib_cs.Color;

namespace LogicSim.Game.Rendering;

public sealed class RenameLayerDialog
{
    public bool IsOpen { get; private set; }
    public bool Confirmed { get; private set; }
    public string NewName { get; private set; } = "";
    public int Target { get; private set; } = -1;

    private RRect _bounds;
    private bool _editing;

    private const int Width = 420;
    private const int Height = 180;
    private const int Pad = 20;
    private const int BtnH = 40;
    private const int MaxName = 16;

    public void Open(string current, int target)
    {
        NewName = current;
        Target = target;
        IsOpen = true;
        Confirmed = false;
        _editing = true;
        _bounds = new RRect((Raylib.GetScreenWidth() - Width) / 2,
            (Raylib.GetScreenHeight() - Height) / 2, Width, Height);
    }

    public void Close()
    {
        IsOpen = false;
        _editing = false;
        Target = -1;
        Confirmed = false;
    }

    private RRect FieldRect()
        => new((int)_bounds.X + Pad, (int)_bounds.Y + Pad + 34, Width - 2 * Pad, 34);

    private static bool Allowed(char ch)
        => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == ' ';

    public void Update()
    {
        if (!IsOpen) return;
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { Close(); return; }
        if (Raylib.IsKeyPressed(KeyboardKey.Enter)) { Confirmed = true; IsOpen = false; return; }

        var m = Raylib.GetMousePosition();
        bool click = Raylib.IsMouseButtonPressed(MouseButton.Left);
        if (click) _editing = Raylib.CheckCollisionPointRec(m, FieldRect());

        int by = (int)_bounds.Y + Height - Pad - BtnH;
        int hw = (Width - 3 * Pad) / 2;
        int bx = (int)_bounds.X + Pad;
        if (click)
        {
            if (Raylib.CheckCollisionPointRec(m, new RRect(bx, by, hw, BtnH)))
            { Confirmed = true; IsOpen = false; return; }
            if (Raylib.CheckCollisionPointRec(m, new RRect(bx + hw + Pad, by, hw, BtnH)))
            { Close(); return; }
        }

        if (!_editing) return;
        int c;
        while ((c = Raylib.GetCharPressed()) != 0)
            if (NewName.Length < MaxName && Allowed((char)c)) NewName += (char)c;
        if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && NewName.Length > 0)
            NewName = NewName[..^1];
    }

    public void Draw()
    {
        if (!IsOpen) return;

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new RColor(0, 0, 0, 170));
        Raylib.DrawRectangleRec(_bounds, new RColor(30, 32, 44, 255));
        Raylib.DrawRectangleLinesEx(_bounds, 2f, new RColor(120, 140, 180, 255));

        int x = (int)_bounds.X + Pad;
        int y = (int)_bounds.Y + Pad;
        Raylib.DrawText("Rename layer", x, y, 20, RColor.White);

        var field = FieldRect();
        Raylib.DrawRectangleRec(field, new RColor(20, 22, 30, 255));
        Raylib.DrawRectangleLinesEx(field, _editing ? 2f : 1f,
            _editing ? RColor.Yellow : new RColor(80, 80, 100, 255));
        string shown = NewName;
        if (_editing && (int)(Raylib.GetTime() * 2) % 2 == 0) shown += "_";
        Raylib.DrawText(shown, x + 8, (int)field.Y + 7, 20, RColor.White);

        int by = (int)_bounds.Y + Height - Pad - BtnH;
        int hw = (Width - 3 * Pad) / 2;
        var ok = new RRect(x, by, hw, BtnH);
        var cancel = new RRect(x + hw + Pad, by, hw, BtnH);
        var m = Raylib.GetMousePosition();

        Raylib.DrawRectangleRec(ok, Raylib.CheckCollisionPointRec(m, ok)
            ? new RColor(90, 170, 90, 255) : new RColor(70, 150, 70, 255));
        Raylib.DrawRectangleLinesEx(ok, 1f, new RColor(140, 220, 140, 255));
        Raylib.DrawText("OK", (int)(ok.X + (ok.Width - Raylib.MeasureText("OK", 20)) / 2),
            (int)ok.Y + 10, 20, RColor.White);

        Raylib.DrawRectangleRec(cancel, Raylib.CheckCollisionPointRec(m, cancel)
            ? new RColor(70, 70, 90, 255) : new RColor(50, 50, 65, 255));
        Raylib.DrawRectangleLinesEx(cancel, 1f, new RColor(90, 90, 110, 255));
        Raylib.DrawText("Cancel",
            (int)(cancel.X + (cancel.Width - Raylib.MeasureText("Cancel", 20)) / 2),
            (int)cancel.Y + 10, 20, RColor.White);
    }
}

public sealed class ConfirmDeleteLayerDialog
{
    public bool IsOpen { get; private set; }
    public bool Confirmed { get; private set; }
    public int Target { get; private set; } = -1;
    public string Message { get; private set; } = "";

    private RRect _bounds;
    private const int Width = 460;
    private const int Height = 210;
    private const int Pad = 20;
    private const int BtnH = 42;

    public void Open(int target, string name, int elements, int wires)
    {
        Target = target;
        Message = $"Delete layer \"{name}\"?\n"
            + $"{elements} element(s), {wires} wire(s) will be removed.\n"
            + "You can undo this action.";
        IsOpen = true;
        Confirmed = false;
        _bounds = new RRect((Raylib.GetScreenWidth() - Width) / 2,
            (Raylib.GetScreenHeight() - Height) / 2, Width, Height);
    }

    public void Close()
    {
        IsOpen = false;
        Target = -1;
        Confirmed = false;
    }

    private (RRect del, RRect cancel) Buttons()
    {
        int by = (int)_bounds.Y + Height - Pad - BtnH;
        int hw = (Width - 3 * Pad) / 2;
        int bx = (int)_bounds.X + Pad;
        return (new RRect(bx, by, hw, BtnH), new RRect(bx + hw + Pad, by, hw, BtnH));
    }

    public void Update()
    {
        if (!IsOpen) return;
        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.Enter))
        { Close(); return; }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;
        var m = Raylib.GetMousePosition();
        var (del, cancel) = Buttons();
        if (Raylib.CheckCollisionPointRec(m, del)) { Confirmed = true; IsOpen = false; return; }
        if (Raylib.CheckCollisionPointRec(m, cancel)) Close();
    }

    public void Draw()
    {
        if (!IsOpen) return;

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new RColor(0, 0, 0, 170));
        Raylib.DrawRectangleRec(_bounds, new RColor(30, 32, 44, 255));
        Raylib.DrawRectangleLinesEx(_bounds, 2f, new RColor(200, 110, 110, 255));

        int x = (int)_bounds.X + Pad;
        int y = (int)_bounds.Y + Pad;
        Raylib.DrawText("Confirm deletion", x, y, 20, RColor.White);
        y += 38;
        foreach (var line in Message.Split('\n'))
        {
            Raylib.DrawText(line, x, y, 16, new RColor(190, 195, 205, 255));
            y += 22;
        }

        var (del, cancel) = Buttons();
        var m = Raylib.GetMousePosition();

        Raylib.DrawRectangleRec(del, Raylib.CheckCollisionPointRec(m, del)
            ? new RColor(180, 75, 75, 255) : new RColor(150, 60, 60, 255));
        Raylib.DrawRectangleLinesEx(del, 1f, new RColor(230, 140, 140, 255));
        Raylib.DrawText("Delete", (int)(del.X + (del.Width - Raylib.MeasureText("Delete", 20)) / 2),
            (int)del.Y + 10, 20, RColor.White);

        Raylib.DrawRectangleRec(cancel, Raylib.CheckCollisionPointRec(m, cancel)
            ? new RColor(70, 70, 90, 255) : new RColor(50, 50, 65, 255));
        Raylib.DrawRectangleLinesEx(cancel, 1f, new RColor(90, 90, 110, 255));
        Raylib.DrawText("Cancel",
            (int)(cancel.X + (cancel.Width - Raylib.MeasureText("Cancel", 20)) / 2),
            (int)cancel.Y + 10, 20, RColor.White);
    }
}
