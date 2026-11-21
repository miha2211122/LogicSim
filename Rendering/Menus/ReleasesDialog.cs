using Raylib_cs;
using RRect = Raylib_cs.Rectangle;
using RColor = Raylib_cs.Color;

namespace LogicSim.Game.Rendering;

/// <summary>Вкладка со списком релизов в главном меню.</summary>
public sealed class ReleasesDialog
{
    private sealed record Release(string Version, string Date, string[] Notes);

    private static readonly Release[] Releases =
    {
        new("v1.0.0", "2025-02-14", new[]
        {
            "10 tutorial levels and progress saving",
            "Macro library and editable chip prototypes",
            "Autosave with Continue in the main menu",
            "Bus-aware macros, wire-chain recalculation and Z fixes"
        }),
        new("v0.9.0", "2025-01-20", new[]
        {
            "Layers: visibility, lock and per-chip layer sets",
            "Pin colors, labels and wire colors",
            "Undo/redo snapshots with 100 steps"
        }),
        new("v0.8.0", "2024-12-05", new[]
        {
            "Macro creation (Ctrl+M) and palette",
            "Multi-bit buses: MERGE / SPLIT",
            "JSON save/load"
        })
    };

    private const int Width = 700;
    private const int Height = 540;
    private const int Pad = 24;
    private const int BtnH = 40;

    private static readonly RColor Panel = new(22, 24, 32, 250);
    private static readonly RColor Border = new(110, 140, 190, 255);
    private static readonly RColor Text = new(240, 244, 252, 255);
    private static readonly RColor Muted = new(170, 185, 210, 255);
    private static readonly RColor Accent = new(80, 140, 210, 255);
    private static readonly RColor AccentHover = new(100, 160, 230, 255);

    private RRect _bounds;
    private RRect _githubBtn;
    private RRect _closeBtn;
    private float _scroll;

    public bool IsOpen { get; private set; }
    public bool CloseRequested { get; private set; }

    public void Open()
    {
        IsOpen = true;
        CloseRequested = false;
        _scroll = 0f;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        _bounds = new RRect((sw - Width) / 2f, (sh - Height) / 2f, Width, Height);
        int by = (int)(_bounds.Y + Height - Pad - BtnH);
        _githubBtn = new RRect(_bounds.X + Pad, by, 220, BtnH);
        _closeBtn = new RRect(_bounds.X + Width - Pad - 120, by, 120, BtnH);
    }

    public void Close()
    {
        IsOpen = false;
        CloseRequested = false;
    }

    public void Update()
    {
        if (!IsOpen) return;

        CloseRequested = false;
        var m = Raylib.GetMousePosition();
        _scroll -= Raylib.GetMouseWheelMove() * 40f;
        _scroll = Math.Clamp(_scroll, 0f, 320f);

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            CloseRequested = true;
            return;
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;

        if (Raylib.CheckCollisionPointRec(m, _closeBtn))
        {
            CloseRequested = true;
        }
        else if (Raylib.CheckCollisionPointRec(m, _githubBtn))
        {
            OpenUrl("https://github.com/miha2211122/LogicSim/releases");
        }
    }

    public void Draw()
    {
        if (!IsOpen) return;

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new RColor(0, 0, 0, 170));
        Raylib.DrawRectangleRec(_bounds, Panel);
        Raylib.DrawRectangleLinesEx(_bounds, 2f, Border);

        Raylib.DrawText("Releases", (int)_bounds.X + Pad, (int)_bounds.Y + Pad, 26, Text);
        Raylib.DrawText("Latest first - scroll for more", (int)_bounds.X + Pad,
            (int)_bounds.Y + Pad + 34, 14, Muted);

        int listTop = (int)_bounds.Y + Pad + 62;
        int listBottom = (int)_githubBtn.Y - 12;
        Raylib.BeginScissorMode((int)_bounds.X + Pad, listTop, Width - Pad * 2, listBottom - listTop);

        int y = listTop - (int)_scroll;
        for (int r = 0; r < Releases.Length; r++)
        {
            var rel = Releases[r];
            Raylib.DrawRectangle((int)_bounds.X + Pad, y, Width - Pad * 2, 22,
                new RColor(45, 55, 75, 220));
            Raylib.DrawText($"{rel.Version}   {rel.Date}", (int)_bounds.X + Pad + 8, y + 3, 18, Text);
            y += 28;

            foreach (var note in rel.Notes)
            {
                Raylib.DrawText($"- {note}", (int)_bounds.X + Pad + 12, y, 15, Muted);
                y += 22;
            }
            y += 18;
        }
        Raylib.EndScissorMode();

        var m = Raylib.GetMousePosition();
        DrawButton(_githubBtn, "Open on GitHub", m);
        DrawButton(_closeBtn, "Close", m);
    }

    private static void DrawButton(RRect r, string text, System.Numerics.Vector2 m)
    {
        bool hover = Raylib.CheckCollisionPointRec(m, r);
        Raylib.DrawRectangleRec(r, hover ? AccentHover : Accent);
        int tw = Raylib.MeasureText(text, 16);
        Raylib.DrawText(text, (int)(r.X + (r.Width - tw) / 2), (int)(r.Y + (r.Height - 16) / 2), 16, Text);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }
}
