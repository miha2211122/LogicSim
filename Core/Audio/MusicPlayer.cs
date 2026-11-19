using Raylib_cs;

namespace LogicSim.Game.Core;

/// <summary>
/// Фоновая музыка из папки Assets: сначала Assets/Music, потом корень Assets.
/// Поддерживаются форматы raylib: mp3, ogg, wav, flac, xm, mod, it, s3m.
/// </summary>
public sealed class MusicPlayer : IDisposable
{
    private static readonly string[] SupportedExtensions =
    {
        ".mp3", ".ogg", ".wav", ".flac", ".xm", ".mod", ".it", ".s3m"
    };

    private const float FadeSeconds = 2f;
    private const float SilenceSeconds = 5f;

    private enum FadePhase { None, FadeOut, Silence, FadeIn }

    private readonly List<string> _tracks = new();
    private Music _current;
    private bool _loaded;
    private bool _enabled = true;
    private int _index = -1;
    private float _volume = 0.35f;

    private FadePhase _phase;
    private double _phaseStart;
    private int _transitionStep;
    private int _queuedStep;

    public MusicPlayer(string assetsDir)
    {
        Scan(assetsDir);
    }

    public string Status
    {
        get
        {
            if (_tracks.Count == 0)
                return "Music: drop .mp3/.ogg/.wav into Assets/Music";

            string name = _index >= 0 ? Path.GetFileName(_tracks[_index]) : "none";
            string state = _phase switch
            {
                FadePhase.FadeOut => "fade out",
                FadePhase.Silence => "silence",
                FadePhase.FadeIn => "fade in",
                _ => _enabled ? "ON" : "OFF"
            };
            return $"Music [{state}]: {name}  F7/F8 vol  F9 play/pause  F10 next";
        }
    }

    public void Load()
    {
        if (_tracks.Count > 0 && TrySelect(0, fadeIn: true))
        {
            _phase = FadePhase.FadeIn;
            _phaseStart = Raylib.GetTime();
        }
    }

    public void Update()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.F7)) ChangeVolume(-0.1f);
        if (Raylib.IsKeyPressed(KeyboardKey.F8)) ChangeVolume(0.1f);
        if (Raylib.IsKeyPressed(KeyboardKey.F9)) Toggle();

        if (Raylib.IsKeyPressed(KeyboardKey.F10))
        {
            if (Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift))
                Prev();
            else
                Next();
        }

        if (_loaded && _enabled)
            Raylib.UpdateMusicStream(_current);

        AdvanceFade();

        // Автопереход, когда трек доиграл (у mod/xm длительность может быть 0 — тогда пропускаем).
        if (_phase == FadePhase.None && _loaded && _enabled && _tracks.Count > 0)
        {
            float length = Raylib.GetMusicTimeLength(_current);
            if (length > 0f && Raylib.GetMusicTimePlayed(_current) >= length - 0.05f)
                StartTransition(1);
        }
    }

    public void Toggle()
    {
        if (!_loaded)
        {
            _enabled = !_enabled;
            return;
        }

        _enabled = !_enabled;
        if (_enabled) Raylib.PlayMusicStream(_current);
        else Raylib.PauseMusicStream(_current);
    }

    public void Next() => RequestStep(1);

    public void Prev() => RequestStep(-1);

    private void RequestStep(int step)
    {
        if (_tracks.Count == 0) return;

        // Повторные нажатия во время перехода не ломают последовательность — запоминаем их.
        if (_phase != FadePhase.None)
        {
            _queuedStep += step;
            return;
        }

        StartTransition(step);
    }

    public void Dispose()
    {
        UnloadCurrent();
    }

    private void ChangeVolume(float delta)
    {
        _volume = Math.Clamp(_volume + delta, 0f, 1f);
        if (_loaded && _phase == FadePhase.None)
            Raylib.SetMusicVolume(_current, _volume);
    }

    private void StartTransition(int step)
    {
        if (_tracks.Count == 0) return;

        if (!_loaded)
        {
            if (TrySelect(_index + step, fadeIn: true))
            {
                _phase = FadePhase.FadeIn;
                _phaseStart = Raylib.GetTime();
            }
            return;
        }

        _transitionStep = step;
        _phase = FadePhase.FadeOut;
        _phaseStart = Raylib.GetTime();
    }

    private void AdvanceFade()
    {
        if (_phase == FadePhase.None) return;

        double now = Raylib.GetTime();

        switch (_phase)
        {
            case FadePhase.FadeOut:
            {
                float t = Clamp01((float)((now - _phaseStart) / FadeSeconds));
                if (_loaded) Raylib.SetMusicVolume(_current, _volume * (1f - t));

                if (t >= 1f)
                {
                    UnloadCurrent();
                    _phase = FadePhase.Silence;
                    _phaseStart = now;
                }
                break;
            }

            case FadePhase.Silence:
            {
                if (now - _phaseStart < SilenceSeconds) break;

                int step = _transitionStep;
                _transitionStep = 0;
                if (TrySelect(_index + step, fadeIn: true))
                {
                    _phase = FadePhase.FadeIn;
                    _phaseStart = now;
                }
                else
                {
                    _phase = FadePhase.None;
                }
                break;
            }

            case FadePhase.FadeIn:
            {
                float t = Clamp01((float)((now - _phaseStart) / FadeSeconds));
                if (_loaded) Raylib.SetMusicVolume(_current, _volume * t);

                if (t >= 1f)
                {
                    _phase = FadePhase.None;
                    if (_queuedStep != 0)
                    {
                        int step = _queuedStep;
                        _queuedStep = 0;
                        StartTransition(step);
                    }
                }
                break;
            }
        }
    }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    private void Scan(string assetsDir)
    {
        string[] dirs = { Path.Combine(assetsDir, "Music"), assetsDir };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories); }
            catch { continue; }

            foreach (string file in files)
            {
                string ext = Path.GetExtension(file);
                if (SupportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase) && seen.Add(file))
                    _tracks.Add(file);
            }
        }

        _tracks.Sort(StringComparer.OrdinalIgnoreCase);
    }

    private bool TrySelect(int start, bool fadeIn = false)
    {
        for (int attempt = 0; attempt < _tracks.Count; attempt++)
        {
            int idx = (start % _tracks.Count + _tracks.Count) % _tracks.Count;
            UnloadCurrent();

            try
            {
                _current = Raylib.LoadMusicStream(_tracks[idx]);
                _loaded = true;
                _index = idx;
                Raylib.SetMusicVolume(_current, fadeIn ? 0f : _volume);
                if (_enabled) Raylib.PlayMusicStream(_current);
                return true;
            }
            catch
            {
                _loaded = false;
                _index = -1;
                start = idx + 1; // битый файл — пробуем следующий
            }
        }

        return false;
    }

    private void UnloadCurrent()
    {
        if (!_loaded) return;

        try { Raylib.StopMusicStream(_current); } catch { }
        try { Raylib.UnloadMusicStream(_current); } catch { }

        _loaded = false;
    }
}
