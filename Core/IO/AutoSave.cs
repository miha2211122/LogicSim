using LogicSim.Game.Rendering;
using System.Numerics;

namespace LogicSim.Game.Core;

/// <summary>
/// Резервная копия активной схемы. Пишется периодически и не заменяет обычный Save As.
/// </summary>
public static class AutoSave
{
    public static string Path { get; } = System.IO.Path.Combine(EditorSavesDir, "autosave.json");

    private static string EditorSavesDir
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return System.IO.Path.Combine(appData, "LogicSim", "Saves");
        }
    }

    public static bool Exists => File.Exists(Path);

    public static DateTime? LastWriteTime
    {
        get
        {
            try { return Exists ? File.GetLastWriteTime(Path) : null; }
            catch { return null; }
        }
    }

    public static void Write(Circuit circuit, Layout layout, Vector2 camTarget, float camZoom,
        List<ChipTemplate> templates, List<ChipTemplate> library, LayerSet layers,
        System.Drawing.Color? recentWireColor, string? sourcePath)
    {
        System.IO.Directory.CreateDirectory(EditorSavesDir);
        string name = !string.IsNullOrEmpty(sourcePath)
            ? System.IO.Path.GetFileNameWithoutExtension(sourcePath)
            : "autosave";
        SaveSystem.Save(Path, circuit, layout, camTarget, camZoom, templates, library,
            name, recentWireColor, layers, sourcePath);
    }

    public static void Clear()
    {
        try { if (Exists) File.Delete(Path); } catch { }
    }
}
