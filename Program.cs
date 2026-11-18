using System.Numerics;
using LogicSim.Game.Core;
using LogicSim.Game.Rendering;
using Raylib_cs;

namespace LogicSim.Game;

public static class Program
{
    private enum GameState { MainMenu, Editor }
    private static GameState _state = GameState.MainMenu;

    private static Editor? _editor;
    private static MainMenu? _mainMenu;
    private static PauseMenu _pauseMenu = new();
    private static GameRenderer? _renderer;
    private static Camera2D _camera;

    private static readonly string SavesDir = GetSavesDir();
    private static string GetSavesDir()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return System.IO.Path.Combine(appData, "LogicSim", "Saves");
    }

    public static void Main()
    {
        const int W = 1280;
        const int H = 720;

        Raylib.InitWindow(W, H, "LogicSim");
        Raylib.SetTargetFPS(60);

        // Отключаем закрытие окна по Esc (0 == KeyboardKey.Null)
        Raylib.SetExitKey(0);

        try
        {
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "xor.png");
            if (System.IO.File.Exists(iconPath))
            {
                var img = Raylib.LoadImage(iconPath);
                Raylib.SetWindowIcon(img);
                Raylib.UnloadImage(img);
            }
        }
        catch { }

        _mainMenu = new MainMenu(SavesDir);

        while (!Raylib.WindowShouldClose())
        {
            if (_state == GameState.MainMenu)
            {
                _mainMenu.Update();

                if (_mainMenu.ExitRequested) break;

                if (_mainMenu.NewGameRequested)
                {
                    CreateNewEditor(W, H);
                    _state = GameState.Editor;
                }
                else if (_mainMenu.LoadGameRequested && _mainMenu.LoadPath != null)
                {
                    CreateNewEditor(W, H);
                    var save = SaveSystem.Load(_mainMenu.LoadPath);
                    if (save != null)
                    {
                        SaveSystem.Apply(save, _editor!.Circuit, _editor.Layout,
                            out var camTarget, out var camZoom,
                            _editor.Palette.Templates, _editor.Palette.Library,
                            _editor.Layers);
                        _camera.Target = camTarget;
                        _camera.Zoom = camZoom;
                        _editor.CurrentSavePath = _mainMenu.LoadPath;
                        _state = GameState.Editor;
                    }
                }

                Raylib.BeginDrawing();
                _mainMenu.Draw();
                Raylib.EndDrawing();
            }
            else if (_state == GameState.Editor && _editor != null && _renderer != null)
            {
                if (_pauseMenu.IsOpen)
                {
                    _pauseMenu.Update();
                    if (_pauseMenu.ResumeRequested) _pauseMenu.Close();
                    if (_pauseMenu.SaveRequested)
                    {
                        if (_editor.CurrentSavePath != null)
                            _editor.SaveCurrent(_camera);
                        else
                            _editor.RequestSave();
                        _pauseMenu.Close();
                    }
                    if (_pauseMenu.MainMenuRequested)
                    {
                        _pauseMenu.Close();
                        _state = GameState.MainMenu;
                        continue;
                    }
                }
                else
                {
                    _editor.Update(ref _camera);
                    if (_editor.IsPaused)
                    {
                        _editor.IsPaused = false;
                        _pauseMenu.Open();
                    }
                }

                _editor.Circuit.Step();

                Raylib.BeginDrawing();
                Raylib.ClearBackground(new Color((byte)18, (byte)18, (byte)24, (byte)255));

                Raylib.BeginMode2D(_camera);
                _renderer.Draw(_editor.Circuit, _editor.Layout, _editor.Layers);
                _renderer.DrawOverlay(_editor, _camera);
                Raylib.EndMode2D();

                // HUD
                Raylib.DrawText("LogicSim", 20, 20, 28, Color.White);

                if (_editor.IsNested)
                {
                    string mode = _editor.IsReadOnly ? "VIEW" : "OPEN";
                    Raylib.DrawText($"[{mode}] {_editor.ContextName}", 20, 55, 20,
                        _editor.IsReadOnly ? Color.Yellow : Color.SkyBlue);
                }

                if (_editor.StatusMessage is { } msg)
                    Raylib.DrawText(msg, 20, 85, 14, Color.Yellow);

                var hudMouse = Raylib.GetMousePosition();
                var saveRect = _editor.SaveButtonRect();
                var loadRect = _editor.LoadButtonRect();
                var backRect = _editor.BackButtonRect();

                if (!_editor.AnyModalOpen && !_pauseMenu.IsOpen && Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    if (Raylib.CheckCollisionPointRec(hudMouse, saveRect))
                        _editor.RequestSave();
                    else if (Raylib.CheckCollisionPointRec(hudMouse, loadRect))
                        _editor.RequestLoad();
                }

                DrawTextButton(saveRect, "Save (Ctrl+S)", hudMouse, new Color((byte)80, (byte)140, (byte)200, (byte)255));
                DrawTextButton(loadRect, "Load (Ctrl+O)", hudMouse, new Color((byte)80, (byte)140, (byte)200, (byte)255));

                if (_editor.DrawBackButton)
                {
                    var lbl = _editor.IsReadOnly ? "< Back (view)" : "< Back";
                    DrawTextButton(backRect, lbl, hudMouse, new Color((byte)100, (byte)130, (byte)180, (byte)255));
                }

                _editor.Palette.Draw();
                _editor.DrawLayerPanel();
                _editor.DrawDialogs();
                if (_editor.HelpVisible) _editor.DrawHelp();

                if (_pauseMenu.IsOpen) _pauseMenu.Draw();

                Raylib.EndDrawing();
            }
        }

        Raylib.CloseWindow();
    }

    private static void CreateNewEditor(int W, int H)
    {
        var circuit = new Circuit();
        var layout = new Layout();

        var palette = new Palette(W, H);
        var contextMenu = new ContextMenu();
        var macroDialog = new MacroDialog();
        var pinEditor = new PinEditorDialog();

        _renderer = new GameRenderer();
        _editor = new Editor(circuit, layout, palette, contextMenu, macroDialog, pinEditor);
        _editor.CurrentSavePath = null;

        _camera = new Camera2D
        {
            Target = new Vector2(W / 2f, H / 2f),
            Offset = new Vector2(W / 2f, H / 2f),
            Rotation = 0f,
            Zoom = 1f
        };
    }

    private static void DrawTextButton(Rectangle r, string text, Vector2 mouse, Color accent)
    {
        bool hover = Raylib.CheckCollisionPointRec(mouse, r);
        var bg = hover ? accent : new Color((byte)50, (byte)55, (byte)75, (byte)230);
        Raylib.DrawRectangleRec(r, bg);
        Raylib.DrawRectangleLinesEx(r, 1f, new Color((byte)120, (byte)130, (byte)160, (byte)255));
        int tw = Raylib.MeasureText(text, 16);
        Raylib.DrawText(text,
            (int)(r.X + (r.Width - tw) / 2),
            (int)(r.Y + (r.Height - 16) / 2),
            16, Color.White);
    }
}