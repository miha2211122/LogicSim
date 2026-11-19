using LogicSim.Game.Core;
using Raylib_cs;
using System.Numerics;
using RColor = Raylib_cs.Color;

namespace LogicSim.Game.Rendering;

public sealed partial class Editor
{
    private static readonly Color[] LayerAccents =
    {
        new((byte)96, (byte)176, (byte)255, (byte)255),
        new((byte)120, (byte)220, (byte)170, (byte)255),
        new((byte)255, (byte)192, (byte)104, (byte)255),
        new((byte)210, (byte)148, (byte)255, (byte)255),
        new((byte)255, (byte)132, (byte)152, (byte)255),
        new((byte)112, (byte)212, (byte)224, (byte)255),
        new((byte)208, (byte)208, (byte)128, (byte)255),
        new((byte)176, (byte)164, (byte)255, (byte)255)
    };

    private const float LayerRowHeight = 28f;
    private int _layerScroll;
    private bool _layersCollapsed;
    private const float LayerHeaderHeight = 46f;
    private const float LayerFooterHeight = 24f;
    private const float LayerCollapsedHeight = 48f;

    private Rectangle LayerPanelRect()
        => new(Raylib.GetScreenWidth() - 292, 118, 276,
            _layersCollapsed
                ? LayerCollapsedHeight
                : Math.Min(LayerHeaderHeight + Ctx.Layers.Count * LayerRowHeight + LayerFooterHeight,
                    Math.Max(180f, Raylib.GetScreenHeight() - 118f - Palette.BarHeight - 16f)));

    private Rectangle LayerPrevRect()
    {
        var p = LayerPanelRect();
        return new Rectangle(p.X + p.Width - 138, p.Y + 10, 26, 26);
    }

    private Rectangle LayerNextRect()
    {
        var p = LayerPanelRect();
        return new Rectangle(p.X + p.Width - 106, p.Y + 10, 26, 26);
    }

    private Rectangle LayerAddRect()
    {
        var p = LayerPanelRect();
        return new Rectangle(p.X + p.Width - 74, p.Y + 10, 26, 26);
    }

    private Rectangle LayerCollapseRect()
    {
        var p = LayerPanelRect();
        return new Rectangle(p.X + p.Width - 40, p.Y + 10, 26, 26);
    }

    private Rectangle LayerRowRect(int i)
    {
        var p = LayerPanelRect();
        return new Rectangle(p.X + 8, p.Y + LayerHeaderHeight + 4 + i * LayerRowHeight - _layerScroll,
            p.Width - 16, LayerRowHeight - 3);
    }

    private Rectangle LayerListRect()
    {
        var p = LayerPanelRect();
        return new Rectangle(p.X + 8, p.Y + LayerHeaderHeight,
            p.Width - 16, Math.Max(0, p.Height - LayerHeaderHeight - LayerFooterHeight));
    }

    private int MaxLayerScroll()
    {
        float content = Ctx.Layers.Count * LayerRowHeight;
        return (int)Math.Max(0, content - LayerListRect().Height);
    }

    private void ClampLayerScroll() =>
        _layerScroll = Math.Clamp(_layerScroll, 0, MaxLayerScroll());

    private void EnsureLayerVisible(int index)
    {
        var area = LayerListRect();
        float top = 4 + index * LayerRowHeight;
        float bottom = top + LayerRowHeight - 3;
        if (top < _layerScroll) _layerScroll = (int)top;
        else if (bottom > _layerScroll + area.Height)
            _layerScroll = (int)(bottom - area.Height);
        ClampLayerScroll();
    }

    private Rectangle LayerDeleteRect(int i)
    {
        var r = LayerRowRect(i);
        return new Rectangle(r.X + r.Width - 28, r.Y + 3, 22, 18);
    }

    private Rectangle LayerVisibleRect(int i)
    {
        var r = LayerRowRect(i);
        return new Rectangle(r.X + r.Width - 80, r.Y + 3, 22, 18);
    }

    private Rectangle LayerLockRect(int i)
    {
        var r = LayerRowRect(i);
        return new Rectangle(r.X + r.Width - 54, r.Y + 3, 22, 18);
    }

    private void OpenLayerRename(int index)
    {
        if (index < 0 || index >= Ctx.Layers.Count) return;
        _layerRename.Open(Ctx.Layers.Layers[index].Name, index);
    }

    private void DeleteLayer(int index)
    {
        if (Ctx.ReadOnly) { StatusMessage = "Read-only context."; _statusTimer = 2f; return; }
        if (Ctx.Layers.Count <= 1 || index < 0 || index >= Ctx.Layers.Count)
        {
            StatusMessage = "Cannot delete the last layer.";
            _statusTimer = 2f;
            return;
        }

        var owners = Ctx.Circuit.Elements.Where(e => e.Layer == index).ToHashSet();
        int wires = Ctx.Circuit.Wires.Count(w =>
            w.Layer == index ||
            (Ctx.Circuit.OwnerOf(w.From) is { } a && owners.Contains(a)) ||
            (Ctx.Circuit.OwnerOf(w.To) is { } b && owners.Contains(b)));
        _layerDeleteConfirm.Open(index, Ctx.Layers.Layers[index].Name, owners.Count, wires);
    }

    private void PerformDeleteLayer(int index)
    {
        if (Ctx.ReadOnly) return;
        if (Ctx.Layers.Count <= 1 || index < 0 || index >= Ctx.Layers.Count) return;

        BeginChange();

        var doomed = Ctx.Circuit.Elements.Where(e => e.Layer == index).ToList();
        foreach (var w in Ctx.Circuit.Wires.Where(w => w.Layer == index).ToList())
            Ctx.Circuit.RemoveWire(w);
        if (doomed.Count > 0)
            Ctx.Circuit.RemoveRange(doomed);

        foreach (var el in Ctx.Circuit.Elements)
            if (el.Layer > index) el.Layer--;
        foreach (var w in Ctx.Circuit.Wires)
            if (w.Layer > index) w.Layer--;

        Ctx.Layers.RemoveLayer(index);
        _selection.RemoveMissing(Ctx.Circuit);
        if (!_layersCollapsed)
        {
            ClampLayerScroll();
            EnsureLayerVisible(Ctx.Layers.Active);
        }
        StatusMessage = $"Deleted layer {index + 1} ({doomed.Count} element(s)).";
        _statusTimer = 2f;
    }

    private bool HandleLayerPanel()
    {
        var m = Raylib.GetMousePosition();
        var list = LayerListRect();
        if (Raylib.CheckCollisionPointRec(m, list))
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0f)
            {
                ClampLayerScroll();
                _layerScroll = Math.Clamp(
                    _layerScroll - (int)(wheel * LayerRowHeight * 3), 0, MaxLayerScroll());
                return true;
            }
        }
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return false;
        if (!Raylib.CheckCollisionPointRec(m, LayerPanelRect())) return false;

        if (Raylib.CheckCollisionPointRec(m, LayerCollapseRect()))
        {
            _layersCollapsed = !_layersCollapsed;
            if (_layersCollapsed) ClampLayerScroll();
            return true;
        }
        if (_layersCollapsed) return true;

        if (Raylib.CheckCollisionPointRec(m, LayerPrevRect()))
        {
            Ctx.Layers.SetActive(Ctx.Layers.Active - 1);
            EnsureLayerVisible(Ctx.Layers.Active);
            return true;
        }
        if (Raylib.CheckCollisionPointRec(m, LayerNextRect()))
        {
            Ctx.Layers.SetActive(Ctx.Layers.Active + 1);
            EnsureLayerVisible(Ctx.Layers.Active);
            return true;
        }
        if (Raylib.CheckCollisionPointRec(m, LayerAddRect()))
        {
            if (Ctx.ReadOnly) { StatusMessage = "Read-only context."; _statusTimer = 2f; return true; }
            BeginChange();
            Ctx.Layers.AddLayer();
            EnsureLayerVisible(Ctx.Layers.Active);
            return true;
        }

        for (int i = 0; i < Ctx.Layers.Count; i++)
        {
            var row = LayerRowRect(i);
            if (row.Y + row.Height < list.Y || row.Y > list.Y + list.Height) continue;
            if (Raylib.CheckCollisionPointRec(m, LayerDeleteRect(i)))
            {
                DeleteLayer(i);
                return true;
            }
            if (Raylib.CheckCollisionPointRec(m, LayerVisibleRect(i)))
            {
                if (Ctx.ReadOnly) { StatusMessage = "Read-only context."; _statusTimer = 2f; return true; }
                BeginChange();
                Ctx.Layers.ToggleVisible(i);
                return true;
            }
            if (Raylib.CheckCollisionPointRec(m, LayerLockRect(i)))
            {
                if (Ctx.ReadOnly) { StatusMessage = "Read-only context."; _statusTimer = 2f; return true; }
                BeginChange();
                Ctx.Layers.ToggleLocked(i);
                return true;
            }
            if (Raylib.CheckCollisionPointRec(m, row))
            {
                double now = Raylib.GetTime();
                if (i == _lastLayerClickIndex && now - _lastLayerClickTime < 0.35)
                {
                    _lastLayerClickIndex = -1;
                    if (!Ctx.ReadOnly) OpenLayerRename(i);
                }
                else
                {
                    Ctx.Layers.SetActive(i);
                    _lastLayerClickIndex = i;
                    _lastLayerClickTime = now;
                }
                return true;
            }
        }
        return true;
    }

    public void DrawLayerPanel()
    {
        var p = LayerPanelRect();
        var m = Raylib.GetMousePosition();
        var accent = LayerAccents[Ctx.Layers.Active % LayerAccents.Length];

        Raylib.DrawRectangleRec(new Rectangle(p.X + 3, p.Y + 4, p.Width, p.Height),
            new Color((byte)0, (byte)0, (byte)0, (byte)90));
        Raylib.DrawRectangleRec(p, new Color((byte)20, (byte)23, (byte)33, (byte)246));
        Raylib.DrawRectangleLinesEx(p, 1f, new Color((byte)72, (byte)92, (byte)122, (byte)255));
        Raylib.DrawRectangle((int)p.X + 1, (int)p.Y, (int)p.Width - 2, 2, accent);

        Raylib.DrawText("LAYERS", (int)p.X + 14, (int)p.Y + 9, 12,
            new Color((byte)140, (byte)155, (byte)180, (byte)255));
        string counter = Ctx.Layers.Count.ToString();
        Raylib.DrawRectangle((int)p.X + 70, (int)p.Y + 8, 40, 16,
            new Color((byte)38, (byte)44, (byte)60, (byte)255));
        Raylib.DrawText(counter, (int)p.X + 74, (int)p.Y + 10, 11, RColor.Gray);

        string activeName = Ctx.Layers.Layers[Ctx.Layers.Active].Name;
        Raylib.DrawText($"{Ctx.Layers.Active + 1}  {activeName}",
            (int)p.X + 14, (int)p.Y + 26, 18, RColor.White);

        if (!_layersCollapsed)
        {
            DrawSmallButton(LayerPrevRect(), "<", Ctx.Layers.Active <= 0, accent);
            DrawSmallButton(LayerNextRect(), ">", Ctx.Layers.Active >= Ctx.Layers.Count - 1, accent);
            DrawSmallButton(LayerAddRect(), "+", false, accent);
        }
        DrawSmallButton(LayerCollapseRect(), _layersCollapsed ? "v" : "^", false, accent);
        if (_layersCollapsed) return;

        Raylib.DrawLine((int)p.X + 8, (int)p.Y + (int)LayerHeaderHeight - 1,
            (int)p.X + (int)p.Width - 8, (int)p.Y + (int)LayerHeaderHeight - 1,
            new Color((byte)58, (byte)70, (byte)92, (byte)255));

        var counts = new int[Ctx.Layers.Count];
        foreach (var el in Ctx.Circuit.Elements)
            if (el.Layer >= 0 && el.Layer < counts.Length) counts[el.Layer]++;
        foreach (var wire in Ctx.Circuit.Wires)
            if (wire.Layer >= 0 && wire.Layer < counts.Length) counts[wire.Layer]++;

        var listRect = LayerListRect();
        ClampLayerScroll();
        Raylib.BeginScissorMode((int)listRect.X, (int)listRect.Y,
            (int)listRect.Width, (int)listRect.Height);
        for (int i = 0; i < Ctx.Layers.Count; i++)
        {
            var row = LayerRowRect(i);
            bool active = i == Ctx.Layers.Active;
            bool hover = Raylib.CheckCollisionPointRec(m, row);
            var layer = Ctx.Layers.Layers[i];
            var rowAccent = LayerAccents[i % LayerAccents.Length];

            Raylib.DrawRectangleRec(row, active
                ? new Color((byte)46, (byte)66, (byte)92, (byte)255)
                : hover ? new Color((byte)38, (byte)44, (byte)60, (byte)255)
                        : new Color((byte)28, (byte)32, (byte)44, (byte)255));
            Raylib.DrawRectangle((int)row.X, (int)row.Y, 3, (int)row.Height,
                layer.Visible ? rowAccent : new Color((byte)90, (byte)95, (byte)110, (byte)255));
            if (active)
                Raylib.DrawRectangleLinesEx(row, 1f,
                    new Color(rowAccent.R, rowAccent.G, rowAccent.B, (byte)210));

            var chip = new Rectangle(row.X + 10, row.Y + 5, 22, 16);
            Raylib.DrawRectangleRec(chip, active
                ? new Color(rowAccent.R, rowAccent.G, rowAccent.B, (byte)70)
                : new Color((byte)40, (byte)45, (byte)60, (byte)255));
            string idx = (i + 1).ToString();
            Raylib.DrawText(idx, (int)(chip.X + (chip.Width - Raylib.MeasureText(idx, 11)) / 2),
                (int)chip.Y + 2, 11, active ? rowAccent : RColor.Gray);

            var nameColor = !layer.Visible ? new Color((byte)110, (byte)115, (byte)130, (byte)255)
                : layer.Locked ? new Color((byte)232, (byte)190, (byte)120, (byte)255)
                : RColor.White;
            Raylib.DrawText(layer.Name, (int)row.X + 40, (int)row.Y + 5, 14, nameColor);

            string count = counts[i].ToString();
            Raylib.DrawText(count, (int)(row.X + row.Width - 88 - Raylib.MeasureText(count, 11)),
                (int)row.Y + 7, 11, RColor.Gray);

            var eyeRect = LayerVisibleRect(i);
            var lockRect = LayerLockRect(i);
            DrawIconButtonBg(eyeRect, Raylib.CheckCollisionPointRec(m, eyeRect));
            DrawIconButtonBg(lockRect, Raylib.CheckCollisionPointRec(m, lockRect));
            DrawEyeIcon(eyeRect, layer.Visible,
                layer.Visible ? new Color((byte)190, (byte)215, (byte)240, (byte)255)
                              : new Color((byte)110, (byte)115, (byte)130, (byte)255));
            DrawLockIcon(lockRect, layer.Locked,
                layer.Locked ? new Color((byte)240, (byte)185, (byte)110, (byte)255)
                             : new Color((byte)130, (byte)138, (byte)155, (byte)255));

            var deleteRect = LayerDeleteRect(i);
            DrawIconButtonBg(deleteRect, Raylib.CheckCollisionPointRec(m, deleteRect));
            Raylib.DrawText("X",
                (int)(deleteRect.X + (deleteRect.Width - Raylib.MeasureText("X", 11)) / 2),
                (int)deleteRect.Y + 3, 11,
                new Color((byte)235, (byte)120, (byte)120, (byte)255));

            if (!layer.Visible)
                Raylib.DrawRectangleRec(row,
                    new Color((byte)20, (byte)23, (byte)33, (byte)191));
        }

        Raylib.EndScissorMode();

        int scrollMax = MaxLayerScroll();
        if (scrollMax > 0)
        {
            float trackX = listRect.X + listRect.Width - 4;
            float thumbH = Math.Max(24f, listRect.Height * listRect.Height /
                Math.Max(1f, Ctx.Layers.Count * LayerRowHeight));
            float thumbY = listRect.Y + (listRect.Height - thumbH) *
                (_layerScroll / (float)scrollMax);
            Raylib.DrawRectangle((int)trackX, (int)listRect.Y, 4, (int)listRect.Height,
                new Color((byte)38, (byte)44, (byte)60, (byte)255));
            Raylib.DrawRectangle((int)trackX, (int)thumbY, 4, (int)thumbH,
                new Color((byte)110, (byte)140, (byte)180, (byte)255));
        }

        Raylib.DrawText("[ ] V K G  F2 rename  Ctrl+Del del",
            (int)p.X + 12, (int)(p.Y + p.Height - LayerFooterHeight + 4),
            11, new Color((byte)120, (byte)130, (byte)150, (byte)255));
    }

    private static void DrawSmallButton(Rectangle r, string label, bool disabled, Color accent)
    {
        var m = Raylib.GetMousePosition();
        bool hover = !disabled && Raylib.CheckCollisionPointRec(m, r);
        Raylib.DrawRectangleRec(r, disabled
            ? new Color((byte)32, (byte)35, (byte)46, (byte)255)
            : hover ? new Color((byte)70, (byte)92, (byte)122, (byte)255)
                    : new Color((byte)42, (byte)48, (byte)64, (byte)255));
        Raylib.DrawRectangleLinesEx(r, 1f, disabled
            ? new Color((byte)60, (byte)64, (byte)78, (byte)255)
            : new Color(accent.R, accent.G, accent.B, (byte)170));

        int tw = Raylib.MeasureText(label, 14);
        Raylib.DrawText(label, (int)(r.X + (r.Width - tw) / 2), (int)(r.Y + (r.Height - 14) / 2), 14,
            disabled ? new Color((byte)90, (byte)95, (byte)110, (byte)255) : RColor.White);
    }

    private static void DrawIconButtonBg(Rectangle r, bool hover)
    {
        if (!hover) return;
        Raylib.DrawRectangleRec(r, new Color((byte)70, (byte)82, (byte)104, (byte)255));
        Raylib.DrawRectangleLinesEx(r, 1f, new Color((byte)130, (byte)155, (byte)190, (byte)255));
    }

    private static void DrawEyeIcon(Rectangle r, bool visible, Color color)
    {
        float cx = r.X + r.Width / 2f;
        float cy = r.Y + r.Height / 2f;
        if (visible)
        {
            Raylib.DrawLineEx(new Vector2(cx - 8, cy), new Vector2(cx - 4, cy - 4), 1.5f, color);
            Raylib.DrawLineEx(new Vector2(cx - 4, cy - 4), new Vector2(cx + 4, cy - 4), 1.5f, color);
            Raylib.DrawLineEx(new Vector2(cx + 4, cy - 4), new Vector2(cx + 8, cy), 1.5f, color);
            Raylib.DrawLineEx(new Vector2(cx + 8, cy), new Vector2(cx + 4, cy + 4), 1.5f, color);
            Raylib.DrawLineEx(new Vector2(cx + 4, cy + 4), new Vector2(cx - 4, cy + 4), 1.5f, color);
            Raylib.DrawLineEx(new Vector2(cx - 4, cy + 4), new Vector2(cx - 8, cy), 1.5f, color);
            Raylib.DrawCircle((int)cx, (int)cy, 2.5f, color);
        }
        else
        {
            Raylib.DrawLineEx(new Vector2(cx - 8, cy), new Vector2(cx, cy + 3), 1.5f, color);
            Raylib.DrawLineEx(new Vector2(cx, cy + 3), new Vector2(cx + 8, cy), 1.5f, color);
            Raylib.DrawLineEx(new Vector2(cx - 6, cy + 6), new Vector2(cx + 6, cy - 6), 1.5f, color);
        }
    }

    private static void DrawLockIcon(Rectangle r, bool locked, Color color)
    {
        float cx = r.X + r.Width / 2f;
        float bodyY = r.Y + 9f;
        float topY = bodyY - 6f;

        Raylib.DrawLineEx(new Vector2(cx - 5, bodyY), new Vector2(cx - 5, topY), 1.5f, color);
        Raylib.DrawLineEx(new Vector2(cx - 5, topY), new Vector2(locked ? cx + 5 : cx + 1, topY), 1.5f, color);
        if (locked)
            Raylib.DrawLineEx(new Vector2(cx + 5, topY), new Vector2(cx + 5, bodyY), 1.5f, color);

        Raylib.DrawRectangle((int)(cx - 7), (int)bodyY, 14, 8, color);
        Raylib.DrawCircle((int)cx, (int)(bodyY + 4), 1.4f,
            new Color((byte)30, (byte)34, (byte)46, (byte)255));
    }
}
