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
    private static MusicPlayer? _music;
    private static Camera2D _camera;

    private static int _screenW;
    private static int _screenH;

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

        // Разрешаем менять размер окна в оконном режиме; fullscreen включается через F11.
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(W, H, "LogicSim");
        _screenW = Raylib.GetScreenWidth();
        _screenH = Raylib.GetScreenHeight();
        Raylib.SetTargetFPS(60);

        // Отключаем закрытие окна по Esc (0 == KeyboardKey.Null)
        Raylib.SetExitKey(0);

        Raylib.InitAudioDevice();
        _music = new MusicPlayer(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets"));
        _music.Load();

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
            HandleFullscreenToggle();
            ReleaseFullscreenOnFocusLoss();
            SyncViewport();

            _music?.Update();

            if (_state == GameState.MainMenu)
            {
                _mainMenu.Update();

                if (_mainMenu.ExitRequested) break;

                if (_mainMenu.NewGameRequested)
                {
                    CreateNewEditor();
                    _state = GameState.Editor;
                }
                else if (_mainMenu.LoadGameRequested && _mainMenu.LoadPath != null)
                {
                    CreateNewEditor();
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
                if (_music != null)
                    Raylib.DrawText(_music.Status, 20, Raylib.GetScreenHeight() - 52, 12, Color.Gray);
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
                _renderer.Draw(_editor.Circuit, _editor.Layout, _editor.Layers, _camera);
                _renderer.DrawOverlay(_editor, _camera);
                Raylib.EndMode2D();

                // HUD
                Raylib.DrawText("LogicSim", 20, 20, 28, Color.White);

                string simLabel = _editor.Circuit.Running ? "RUN" : "PAUSED";
                Raylib.DrawText(simLabel, 20, 112, 18,
                    _editor.Circuit.Running
                        ? new Color((byte)120, (byte)220, (byte)120, (byte)255)
                        : new Color((byte)240, (byte)200, (byte)110, (byte)255));
                Raylib.DrawText("Space pause/play   . step   T timing   F11 fullscreen", 20, 134, 12, Color.Gray);
                if (_music != null)
                    Raylib.DrawText(_music.Status, 20, 156, 12,
                        new Color((byte)150, (byte)170, (byte)200, (byte)255));

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

        _music?.Dispose();
        Raylib.CloseAudioDevice();
        Raylib.CloseWindow();
    }

    private static void CreateNewEditor()
    {
        int w = Raylib.GetScreenWidth();
        int h = Raylib.GetScreenHeight();
        var circuit = new Circuit();
        var layout = new Layout();

        var palette = new Palette(w, h);
        var contextMenu = new ContextMenu();
        var macroDialog = new MacroDialog();
        var pinEditor = new PinEditorDialog();

        _renderer = new GameRenderer();
        _editor = new Editor(circuit, layout, palette, contextMenu, macroDialog, pinEditor);
        _editor.CurrentSavePath = null;

        _camera = new Camera2D
        {
            Target = new Vector2(w / 2f, h / 2f),
            Offset = new Vector2(w / 2f, h / 2f),
            Rotation = 0f,
            Zoom = 1f
        };
    }

    /// <summary>
    /// F11 / Alt+Enter. В raylib это ConfigFlags.FullscreenMode: на Windows переключается
    /// видеорежим монитора, окно занимает экран целиком, без рамки и панели задач.
    /// </summary>
    private static void HandleFullscreenToggle()
    {
        bool f11 = Raylib.IsKeyPressed(KeyboardKey.F11);
        bool altEnter = Raylib.IsKeyPressed(KeyboardKey.Enter) &&
                        (Raylib.IsKeyDown(KeyboardKey.LeftAlt) ||
                         Raylib.IsKeyDown(KeyboardKey.RightAlt));
        if (!f11 && !altEnter) return;

        Raylib.ToggleFullscreen();
    }

    /// <summary>
    /// Alt+Tab / Win / клик по другому окну: снимаем эксклюзивный fullscreen,
    /// иначе монитор остаётся захваченным и другие окна не видны.
    /// </summary>
    private static void ReleaseFullscreenOnFocusLoss()
    {
        if (!Raylib.IsWindowFullscreen() || Raylib.IsWindowFocused()) return;

        Raylib.ToggleFullscreen();
    }

    /// <summary>Подгоняем UI и камеру под реальный размер экрана (в т.ч. при входе/выходе из fullscreen).</summary>
    private static void SyncViewport()
    {
        int w = Raylib.GetScreenWidth();
        int h = Raylib.GetScreenHeight();
        if (w == _screenW && h == _screenH) return;

        _screenW = w;
        _screenH = h;
        _editor?.Palette.Resize(w, h);
        _camera.Offset = new Vector2(w / 2f, h / 2f);
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