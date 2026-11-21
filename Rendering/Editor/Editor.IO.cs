using LogicSim.Game.Core;
using Raylib_cs;

namespace LogicSim.Game.Rendering;

public sealed partial class Editor
{
    private static readonly string SavesDir = GetSavesDir();
    private static string GetSavesDir()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return System.IO.Path.Combine(appData, "LogicSim", "Saves");
    }

    private void DoSave(string path, Camera2D camera)
    {
        try
        {
            System.IO.Directory.CreateDirectory(SavesDir);
            var root = _stack.Last();
            SaveSystem.Save(path, root.Circuit, root.Layout,
                camera.Target, camera.Zoom,
                _palette.Templates, _palette.Library,
                System.IO.Path.GetFileNameWithoutExtension(path),
                _recentWireColor,
                root.Layers);

            CurrentSavePath = path;
            AutoSave.Clear();
            StatusMessage = $"Saved -> {path}";
        }
        catch (Exception ex) { StatusMessage = $"Save error: {ex.Message}"; }
        _statusTimer = 5f;
    }

    private void DoLoad(string path, Camera2D camera)
    {
        try
        {
            var save = SaveSystem.Load(path);
            if (save is null) { StatusMessage = $"No file: {path}"; _statusTimer = 5f; return; }

            while (_stack.Count > 1) _stack.Pop();
            var root = _stack.Peek();
            root.History.Clear();
            _chipHistories.Clear();
            _dragSnapshot = null;

            SaveSystem.Apply(save, root.Circuit, root.Layout,
                out var camTarget, out var camZoom,
                _palette.Templates, _palette.Library,
                root.Layers);

            camera.Target = camTarget;
            camera.Zoom = camZoom;

            _recentWireColor = SaveSystem.FromHexOrNull(save.RecentWireColorHex);
            _clipboard.CancelHologram();

            CurrentSavePath = path;
            AutoSave.Clear();
            StatusMessage = $"Loaded <- {path}";
        }
        catch (Exception ex) { StatusMessage = $"Load error: {ex.Message}"; }
        _statusTimer = 5f;
    }

    public void RestoreRecentWireColor(SaveFile save) =>
        _recentWireColor = SaveSystem.FromHexOrNull(save.RecentWireColorHex);

    /// <summary>Фоновая резервная копия корневой схемы. Ошибки записи не должны мешать игре.</summary>
    public void WriteAutoSave(Camera2D camera)
    {
        try
        {
            var root = _stack.Last();
            AutoSave.Write(root.Circuit, root.Layout, camera.Target, camera.Zoom,
                _palette.Templates, _palette.Library, root.Layers, _recentWireColor, CurrentSavePath);
        }
        catch { }
    }
}
