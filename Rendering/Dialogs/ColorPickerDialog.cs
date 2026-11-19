using Raylib_cs;
using System.Numerics;
using RRect = Raylib_cs.Rectangle;
using RColor = Raylib_cs.Color;
using SDColor = System.Drawing.Color;

namespace LogicSim.Game.Rendering;

/// <summary>
/// Диалог выбора произвольного цвета: три горизонтальных слайдера
/// (Hue / Saturation / Value) + свотч-превью. Работает через DrawRectangle,
/// без Image API — чтобы не зависеть от версии Raylib-cs.
/// </summary>
public sealed class ColorPickerDialog
{
    public bool IsOpen { get; private set; }
    public bool Confirmed { get; private set; }
    public SDColor SelectedColor { get; private set; } = SDColor.FromArgb(80, 220, 80);

    private float _h, _s, _v;

    private const int Width = 460;
    private const int Height = 380;
    private const int Pad = 20;
    private const int BarH = 24;
    private const int BtnH = 40;
    private const int PreviewH = 60;
    private const int LabelW = 90;
    private const int RowGap = 22;

    private RRect _bounds;
    private RRect _hueRect;
    private RRect _satRect;
    private RRect _valRect;
    private RRect _previewRect;

    private string _title = "Color";

    // -1 = ничего, 0 = hue, 1 = saturation, 2 = value
    private int _dragging = -1;

    public void Open(string title, SDColor initial)
    {
        _title = title;
        IsOpen = true;
        Confirmed = false;

        SelectedColor = initial;
        RgbToHsv(initial, out _h, out _s, out _v);

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        _bounds = new RRect((sw - Width) / 2, (sh - Height) / 2, Width, Height);

        int x = (int)_bounds.X + Pad + LabelW;
        int y = (int)_bounds.Y + Pad + 40;
        int w = Width - Pad * 2 - LabelW;

        _hueRect = new RRect(x, y, w, BarH); y += BarH + RowGap;
        _satRect = new RRect(x, y, w, BarH); y += BarH + RowGap;
        _valRect = new RRect(x, y, w, BarH); y += BarH + RowGap + 6;

        _previewRect = new RRect((int)_bounds.X + Pad, y, Width - 2 * Pad, PreviewH);

        _dragging = -1;
    }

    public void Close()
    {
        IsOpen = false;
        _dragging = -1;
    }

    public void Update()
    {
        if (!IsOpen) return;

        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { Close(); return; }

        var m = Raylib.GetMousePosition();
        bool down = Raylib.IsMouseButtonDown(MouseButton.Left);
        bool pressed = Raylib.IsMouseButtonPressed(MouseButton.Left);
        bool released = Raylib.IsMouseButtonReleased(MouseButton.Left);

        if (pressed)
        {
            if (Raylib.CheckCollisionPointRec(m, _hueRect)) _dragging = 0;
            else if (Raylib.CheckCollisionPointRec(m, _satRect)) _dragging = 1;
            else if (Raylib.CheckCollisionPointRec(m, _valRect)) _dragging = 2;
        }
        if (released) _dragging = -1;

        if (down && _dragging >= 0)
        {
            var r = _dragging switch
            {
                0 => _hueRect,
                1 => _satRect,
                _ => _valRect
            };
            float t = Math.Clamp((m.X - r.X) / r.Width, 0f, 1f);

            if (_dragging == 0) _h = t * 360f;
            else if (_dragging == 1) _s = t;
            else _v = t;

            Recompute();
        }

        int by = (int)_bounds.Y + Height - Pad - BtnH;
        int halfW = (Width - 3 * Pad) / 2;
        var okRect = new RRect((int)_bounds.X + Pad, by, halfW, BtnH);
        var cancelRect = new RRect((int)_bounds.X + Pad + halfW + Pad, by, halfW, BtnH);

        if (pressed)
        {
            if (Raylib.CheckCollisionPointRec(m, okRect))
            {
                Confirmed = true;
                IsOpen = false;
                return;
            }
            if (Raylib.CheckCollisionPointRec(m, cancelRect))
            {
                Close();
                return;
            }
        }
    }

    public void Draw()
    {
        if (!IsOpen) return;

        // Затемнение фона
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new RColor((byte)0, (byte)0, (byte)0, (byte)170));

        // Окно
        Raylib.DrawRectangleRec(_bounds, new RColor((byte)30, (byte)32, (byte)44, (byte)255));
        Raylib.DrawRectangleLinesEx(_bounds, 2f, new RColor((byte)120, (byte)140, (byte)180, (byte)255));

        int x0 = (int)_bounds.X + Pad;
        int y0 = (int)_bounds.Y + Pad;

        Raylib.DrawText(_title, x0, y0, 22, RColor.White);

        // ─── Hue slider ───
        DrawLabel("Hue", _hueRect);
        DrawHueBar(_hueRect);
        DrawMarker(_hueRect, _h / 360f);

        // ─── Saturation slider ───
        DrawLabel("Saturation", _satRect);
        DrawSatBar(_satRect);
        DrawMarker(_satRect, _s);

        // ─── Value slider ───
        DrawLabel("Brightness", _valRect);
        DrawValBar(_valRect);
        DrawMarker(_valRect, _v);

        // ─── Preview ───
        Raylib.DrawRectangleRec(_previewRect,
            new RColor(SelectedColor.R, SelectedColor.G, SelectedColor.B, (byte)255));
        Raylib.DrawRectangleLinesEx(_previewRect, 1f, new RColor((byte)150, (byte)150, (byte)180, (byte)255));

        string hex = $"#{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";
        int hw = Raylib.MeasureText(hex, 20);
        Raylib.DrawText(hex,
            (int)(_previewRect.X + _previewRect.Width - hw - 12),
            (int)(_previewRect.Y + (_previewRect.Height - 20) / 2),
            20, RColor.White);

        // ─── Кнопки ───
        int by = (int)_bounds.Y + Height - Pad - BtnH;
        int halfW = (Width - 3 * Pad) / 2;
        var okRect = new RRect((int)_bounds.X + Pad, by, halfW, BtnH);
        var cancelRect = new RRect((int)_bounds.X + Pad + halfW + Pad, by, halfW, BtnH);

        var m = Raylib.GetMousePosition();

        bool okHover = Raylib.CheckCollisionPointRec(m, okRect);
        Raylib.DrawRectangleRec(okRect, okHover
            ? new RColor((byte)90, (byte)170, (byte)90, (byte)255)
            : new RColor((byte)70, (byte)150, (byte)70, (byte)255));
        Raylib.DrawRectangleLinesEx(okRect, 1f, new RColor((byte)140, (byte)220, (byte)140, (byte)255));
        int otw = Raylib.MeasureText("OK", 20);
        Raylib.DrawText("OK", (int)(okRect.X + (okRect.Width - otw) / 2), (int)(okRect.Y + 10), 20, RColor.White);

        bool cHover = Raylib.CheckCollisionPointRec(m, cancelRect);
        Raylib.DrawRectangleRec(cancelRect, cHover
            ? new RColor((byte)70, (byte)70, (byte)90, (byte)255)
            : new RColor((byte)50, (byte)50, (byte)65, (byte)255));
        Raylib.DrawRectangleLinesEx(cancelRect, 1f, new RColor((byte)90, (byte)90, (byte)110, (byte)255));
        int ctw = Raylib.MeasureText("Cancel", 20);
        Raylib.DrawText("Cancel", (int)(cancelRect.X + (cancelRect.Width - ctw) / 2), (int)(cancelRect.Y + 10), 20, RColor.White);
    }

    private static void DrawLabel(string text, RRect r)
    {
        Raylib.DrawText(text, (int)r.X - LabelW + 6, (int)(r.Y + (r.Height - 16) / 2), 16, RColor.LightGray);
    }

    private static void DrawMarker(RRect r, float t)
    {
        int mx = (int)(r.X + t * r.Width);
        Raylib.DrawRectangle(mx - 2, (int)r.Y - 4, 4, (int)r.Height + 8, RColor.White);
        Raylib.DrawRectangleLines(mx - 2, (int)r.Y - 4, 4, (int)r.Height + 8, RColor.Black);
    }

    private static void DrawHueBar(RRect r)
    {
        for (int i = 0; i < (int)r.Width; i++)
        {
            float t = i / r.Width;
            var c = HsvToRgb(t * 360f, 1f, 1f);
            Raylib.DrawRectangle((int)r.X + i, (int)r.Y, 1, (int)r.Height,
                new RColor(c.R, c.G, c.B, (byte)255));
        }
        Raylib.DrawRectangleLinesEx(r, 1f, new RColor((byte)120, (byte)120, (byte)150, (byte)255));
    }

    private void DrawSatBar(RRect r)
    {
        for (int i = 0; i < (int)r.Width; i++)
        {
            float t = i / r.Width;
            var c = HsvToRgb(_h, t, _v <= 0f ? 1f : _v);
            Raylib.DrawRectangle((int)r.X + i, (int)r.Y, 1, (int)r.Height,
                new RColor(c.R, c.G, c.B, (byte)255));
        }
        Raylib.DrawRectangleLinesEx(r, 1f, new RColor((byte)120, (byte)120, (byte)150, (byte)255));
    }

    private void DrawValBar(RRect r)
    {
        for (int i = 0; i < (int)r.Width; i++)
        {
            float t = i / r.Width;
            var c = HsvToRgb(_h, _s, t);
            Raylib.DrawRectangle((int)r.X + i, (int)r.Y, 1, (int)r.Height,
                new RColor(c.R, c.G, c.B, (byte)255));
        }
        Raylib.DrawRectangleLinesEx(r, 1f, new RColor((byte)120, (byte)120, (byte)150, (byte)255));
    }

    private void Recompute() => SelectedColor = HsvToRgb(_h, _s, _v);

    private static SDColor HsvToRgb(float h, float s, float v)
    {
        h = ((h % 360f) + 360f) % 360f;
        float c = v * s;
        float xx = c * (1 - MathF.Abs((h / 60f) % 2 - 1));
        float m = v - c;

        float r, g, b;
        if (h < 60) { r = c; g = xx; b = 0; }
        else if (h < 120) { r = xx; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = xx; }
        else if (h < 240) { r = 0; g = xx; b = c; }
        else if (h < 300) { r = xx; g = 0; b = c; }
        else { r = c; g = 0; b = xx; }

        return SDColor.FromArgb(
            Math.Clamp((int)MathF.Round((r + m) * 255f), 0, 255),
            Math.Clamp((int)MathF.Round((g + m) * 255f), 0, 255),
            Math.Clamp((int)MathF.Round((b + m) * 255f), 0, 255));
    }

    private static void RgbToHsv(SDColor c, out float h, out float s, out float v)
    {
        float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float d = max - min;

        v = max;
        s = max <= 0f ? 0f : d / max;

        if (d <= 0.0001f) { h = 0f; return; }
        if (max == r) h = 60f * (((g - b) / d) % 6f);
        else if (max == g) h = 60f * (((b - r) / d) + 2f);
        else h = 60f * (((r - g) / d) + 4f);
        if (h < 0) h += 360f;
    }
}