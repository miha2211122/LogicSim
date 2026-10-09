using LogicSim.Game.Core;
using Raylib_cs;
using System.Numerics;

namespace LogicSim.Game.Rendering;

public sealed class SelectionController
{
    private readonly HashSet<Element> _selected = new();
    private readonly Dictionary<Element, Vector2> _dragStartPositions = new();

    private Vector2 _boxStart;
    private Vector2 _dragStartMouse;
    private bool _boxSelecting;
    private bool _additive;
    private bool _dragging;
    private bool _hadInitialOverlap;

    public IReadOnlyCollection<Element> Selected => _selected;
    public bool IsBoxSelecting => _boxSelecting;
    public bool IsAdditive => _additive;
    public bool IsDragging => _dragging;
    public Vector2 BoxStart => _boxStart;

    public void Clear() => _selected.Clear();

    public void Toggle(Element el)
    {
        if (_selected.Contains(el)) _selected.Remove(el);
        else _selected.Add(el);
    }

    public void SetSingle(Element el)
    {
        _selected.Clear();
        _selected.Add(el);
    }

    public void RemoveMissing(Circuit circuit)
        => _selected.RemoveWhere(el => !circuit.Elements.Contains(el));

    public void BeginBox(Vector2 world, bool additive)
    {
        _boxStart = world;
        _boxSelecting = true;
        _additive = additive;
    }

    public void EndBox(Circuit circuit, Layout layout, Vector2 world)
    {
        if (!_boxSelecting) return;

        var rect = BoxRect(world);
        if (!_additive) _selected.Clear();

        foreach (var el in circuit.Elements)
        {
            if (!layout.TryGet(el, out var p)) continue;
            if (Raylib.CheckCollisionRecs(rect, new Rectangle(p.X, p.Y, el.Width, el.Height)))
                _selected.Add(el);
        }

        _boxSelecting = false;
        _additive = false;
    }

    public Rectangle BoxRect(Vector2 world)
    {
        float x = MathF.Min(_boxStart.X, world.X);
        float y = MathF.Min(_boxStart.Y, world.Y);
        return new Rectangle(x, y,
            MathF.Abs(_boxStart.X - world.X),
            MathF.Abs(_boxStart.Y - world.Y));
    }

    public void BeginDrag(Layout layout, Circuit circuit, Element underCursor, Vector2 mouseWorld)
    {
        _dragStartPositions.Clear();
        _dragging = true;
        _dragStartMouse = mouseWorld;

        if (_selected.Contains(underCursor))
        {
            foreach (var el in _selected)
                if (layout.TryGet(el, out var p))
                    _dragStartPositions[el] = new Vector2(p.X, p.Y);
        }
        else
        {
            SetSingle(underCursor);
            if (layout.TryGet(underCursor, out var p))
                _dragStartPositions[underCursor] = new Vector2(p.X, p.Y);
        }

        _hadInitialOverlap = HasOverlap(layout, circuit, _dragStartPositions);
    }

    public void UpdateDrag(Layout layout, Circuit circuit, Vector2 mouseWorld)
    {
        if (!_dragging || _dragStartPositions.Count == 0) return;

        var delta = mouseWorld - _dragStartMouse;

        var candidates = new Dictionary<Element, Vector2>();
        foreach (var kv in _dragStartPositions)
            candidates[kv.Key] = kv.Value + delta;

        if (!_hadInitialOverlap && HasOverlap(layout, circuit, candidates))
            return;

        foreach (var kv in candidates)
            layout.Place(kv.Key, (int)kv.Value.X, (int)kv.Value.Y);
    }

    public void EndDrag()
    {
        _dragging = false;
        _dragStartPositions.Clear();
        _hadInitialOverlap = false;
    }

    private static bool HasOverlap(Layout layout, Circuit circuit, Dictionary<Element, Vector2> positions)
    {
        foreach (var kv in positions)
        {
            var el = kv.Key;
            var r = new Rectangle(kv.Value.X, kv.Value.Y, el.Width, el.Height);
            foreach (var other in circuit.Elements)
            {
                if (positions.ContainsKey(other)) continue;
                if (!layout.TryGet(other, out var op)) continue;
                if (Raylib.CheckCollisionRecs(r, new Rectangle(op.X, op.Y, other.Width, other.Height)))
                    return true;
            }
        }
        return false;
    }
}