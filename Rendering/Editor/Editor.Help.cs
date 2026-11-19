using Raylib_cs;

namespace LogicSim.Game.Rendering;

public sealed partial class Editor
{
    public void DrawHelp()
    {
        if (!_helpVisible) return;

        string[] lines =
        {
            "F1        toggle this help",
            "1..3      IN / OUT / NAND (repeat = +1)",
            "4..7      MERGE / SPLIT / CLK / DELAY (from library)",
            "Ctrl+L    library: PARTS (3-STATE, PULL UP/DOWN) and MACROS",
            "0         clear active tool",
            "Space     pause / resume simulation (time only)",
            ".         single tick (works while paused)",
            "T         edit CLK period / DELAY under cursor",
            "Ctrl+S    save      Ctrl+O    load",
            "Ctrl+C    copy      Ctrl+V    paste (click to place)",
            "Ctrl+D    duplicate selection (click to place)",
            "Ctrl+A    select all        Ctrl+Shift+A    deselect",
            "Ctrl+M    make macro        Ctrl+L    macro library",
            "Ctrl+Z    undo      Ctrl+Y / Ctrl+Shift+Z    redo",
            "Delete    delete selected / element under cursor",
            "Esc       cancel wiring / pause (root) / leave chip",
            "Shift+LMB box / additive select",
            "Wheel     zoom at cursor   + / -  zoom at center",
            "Home      reset camera     MMB or LMB drag on empty = pan",
            "RMB       pin / element / wire context menu",
            "[ / ]     previous / next active layer",
            "V         hide / show active layer",
            "K         lock / unlock active layer",
            "G         move selection to active layer",
            "+ panel   add layer / pick layer visibility and lock"
        };

        const int w = 560;
        int rowH = 26;
        int h = 70 + lines.Length * rowH + 20;
        var bounds = new Rectangle(
            (Raylib.GetScreenWidth() - w) / 2f,
            (Raylib.GetScreenHeight() - h) / 2f, w, h);

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new Color((byte)0, (byte)0, (byte)0, (byte)170));
        Raylib.DrawRectangleRec(bounds, new Color((byte)22, (byte)24, (byte)32, (byte)250));
        Raylib.DrawRectangleLinesEx(bounds, 2f, new Color((byte)110, (byte)140, (byte)190, (byte)255));

        Raylib.DrawText("LogicSim hotkeys", (int)bounds.X + 20, (int)bounds.Y + 16, 24, Color.White);

        for (int i = 0; i < lines.Length; i++)
            Raylib.DrawText(lines[i], (int)bounds.X + 20, (int)bounds.Y + 56 + i * rowH, 16,
                new Color((byte)200, (byte)215, (byte)240, (byte)255));
    }
}
