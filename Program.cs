using System.Numerics;
using LogicSim.Game.Core;
using LogicSim.Game.Rendering;
using Raylib_cs;

namespace LogicSim.Game;

public static class Program
{
    public static void Main()
    {
        const int W = 1280;
        const int H = 720;

        Raylib.InitWindow(W, H, "LogicSim - NAND only");
        Raylib.SetTargetFPS(60);

        // Иконка окна
        try
        {
            if (File.Exists("icon.png"))
            {
                var img = Raylib.LoadImage("icon.png");
                Raylib.SetWindowIcon(img);
                Raylib.UnloadImage(img);
            }
        }
        catch { /* пропускаем */ }

        var circuit = new Circuit();
        var a = circuit.Add(new InElement());
        var b = circuit.Add(new InElement());
        var nand = circuit.Add(new NandElement());
        var outEl = circuit.Add(new OutElement());

        circuit.Connect(a.Out, nand.A);
        circuit.Connect(b.Out, nand.B);
        circuit.Connect(nand.Out, outEl.In);

        var layout = new Layout();
        layout.Place(a, 180, 260);
        layout.Place(b, 180, 420);
        layout.Place(nand, 560, 340);
        layout.Place(outEl, 940, 340);

        var palette = new Palette(W, H);
        var contextMenu = new ContextMenu();
        var macroDialog = new MacroDialog();
        var pinEditor = new PinEditorDialog();
        var renderer = new GameRenderer();
        var editor = new Editor(circuit, layout, palette, contextMenu, macroDialog, pinEditor);

        var camera = new Camera2D
        {
            Target = new Vector2(W / 2f, H / 2f),
            Offset = new Vector2(W / 2f, H / 2f),
            Rotation = 0f,
            Zoom = 1f
        };

        while (!Raylib.WindowShouldClose())
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0f && !editor.AnyModalOpen)
            {
                var zm = Raylib.GetMousePosition();
                var wb = Raylib.GetScreenToWorld2D(zm, camera);
                camera.Zoom = Math.Clamp(camera.Zoom + wheel * 0.1f, 0.25f, 4f);
                var wa = Raylib.GetScreenToWorld2D(zm, camera);
                camera.Target += wb - wa;
            }

            var hudMouse = Raylib.GetMousePosition();
            var saveRect = editor.SaveButtonRect();
            var loadRect = editor.LoadButtonRect();
            var backRect = editor.BackButtonRect();

            if (!editor.AnyModalOpen && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                if (Raylib.CheckCollisionPointRec(hudMouse, saveRect))
                    editor.RequestSave();
                else if (Raylib.CheckCollisionPointRec(hudMouse, loadRect))
                    editor.RequestLoad();
            }

            editor.Update(ref camera);
            editor.Circuit.Step();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color((byte)18, (byte)18, (byte)24, (byte)255));

            Raylib.BeginMode2D(camera);
            renderer.Draw(editor.Circuit, editor.Layout);
            renderer.DrawOverlay(editor, camera);
            Raylib.EndMode2D();

            Raylib.DrawText("NAND simulator", 20, 20, 28, Color.White);
            Raylib.DrawText("Pan: MMB or LMB on empty | Zoom: wheel | Shift+LMB: box select | Del: delete",
                20, 55, 16, Color.LightGray);

            if (editor.IsNested)
            {
                string mode = editor.IsReadOnly ? "VIEW" : "OPEN";
                Raylib.DrawText($"[{mode}] {editor.ContextName}", 20, 80, 20,
                    editor.IsReadOnly ? Color.Yellow : Color.SkyBlue);
            }
            if (editor.StatusMessage is { } msg)
                Raylib.DrawText(msg, 20, 105, 14, Color.Yellow);

            DrawTextButton(saveRect, "Save (Ctrl+S)", hudMouse, new Color((byte)80, (byte)140, (byte)200, (byte)255));
            DrawTextButton(loadRect, "Load (Ctrl+O)", hudMouse, new Color((byte)80, (byte)140, (byte)200, (byte)255));

            if (editor.DrawBackButton)
            {
                var lbl = editor.IsReadOnly ? "< Back (view)" : "< Back";
                DrawTextButton(backRect, lbl, hudMouse, new Color((byte)100, (byte)130, (byte)180, (byte)255));
            }

            palette.Draw();
            contextMenu.Draw();
            macroDialog.Draw();
            pinEditor.Draw();
            editor.DrawDialogs();

            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }

    private static void DrawTextButton(Rectangle r, string text, Vector2 mouse, Color accent)
    {
        bool hover = Raylib.CheckCollisionPointRec(mouse, r);
        var bg = hover ? accent : new Color((byte)50, (byte)55, (byte)75, (byte)230);
        Raylib.DrawRectangleRec(r, bg);
        Raylib.DrawRectangleLinesEx(r, 1f, new Color((byte)120, (byte)130, (byte)160, (byte)255));
        int tw = Raylib.MeasureText(text, 16);
        Raylib.DrawText(text, (int)(r.X + (r.Width - tw) / 2), (int)(r.Y + (r.Height - 16) / 2), 16, Color.White);
    }
}