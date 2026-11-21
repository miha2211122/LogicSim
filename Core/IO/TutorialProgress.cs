using System.Text.Json;

namespace LogicSim.Game.Core;

/// <summary>Одна попытка проверки уровня в истории прохождений.</summary>
public sealed class TutorialAttempt
{
    public int Level { get; set; }
    public bool Success { get; set; }
    public string At { get; set; } = "";
    public int Seconds { get; set; }
}

public sealed class TutorialCompletion
{
    public int Level { get; set; }
    public string At { get; set; } = "";
    public int Seconds { get; set; }
}

/// <summary>Прогресс обучения. Хранится отдельно от схем, чтобы не блокироваться их загрузкой.</summary>
public sealed class TutorialProgress
{
    public int Version { get; set; } = 1;
    public List<TutorialCompletion> Completed { get; set; } = new();
    public List<TutorialAttempt> History { get; set; } = new();

    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LogicSim", "TutorialProgress.json");

    public bool IsCompleted(int level) => Completed.Any(c => c.Level == level);
    public int CompletedCount => Completed.Select(c => c.Level).Distinct().Count();

    /// <summary>Песочница открывается только после всех десяти уровней.</summary>
    public bool IsUnlocked => Enumerable.Range(1, 10).All(IsCompleted);

    public static TutorialProgress Load()
    {
        try
        {
            if (!File.Exists(Path)) return new TutorialProgress();
            var p = JsonSerializer.Deserialize<TutorialProgress>(File.ReadAllText(Path));
            if (p is null) return new TutorialProgress();
            p.Completed ??= new();
            p.History ??= new();
            return p;
        }
        catch
        {
            return new TutorialProgress();
        }
    }

    public void Save()
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(Path, JsonSerializer.Serialize(this));
        }
        catch { /* обучение не должно падать из-за недоступного диска */ }
    }

    public void Record(int level, bool success, int seconds)
    {
        string at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        History.Insert(0, new TutorialAttempt { Level = level, Success = success, At = at, Seconds = seconds });
        if (History.Count > 200) History.RemoveRange(200, History.Count - 200);

        if (success && !IsCompleted(level))
            Completed.Add(new TutorialCompletion { Level = level, At = at, Seconds = seconds });

        Save();
    }
}
