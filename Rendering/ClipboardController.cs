using LogicSim.Game.Core;
using Raylib_cs;
using System.Numerics;

namespace LogicSim.Game.Rendering;

public sealed class ClipboardController
{
    public sealed class WireEntry
    {
        public int FromIdx;
        public int FromPinIdx;
        public int ToIdx;
        public int ToPinIdx;
        public List<Vector2> Waypoints = new();
    }

    public sealed class Data
    {
        public List<Element> Elements = new();
        public List<Vector2> Positions = new();
        public List<WireEntry> Wires = new();
    }

    private Data? _clipboard;
    private Data? _hologram;
    private Vector2 _center;

    public Data? Hologram => _hologram;
    public Vector2 HologramCenter => _center;
    public bool HasClipboard => _clipboard is not null && _clipboard.Elements.Count > 0;

    public void CancelHologram() => _hologram = null;

    public void CopyFrom(Circuit circuit, Layout layout, IReadOnlyCollection<Element> selection)
    {
        if (selection.Count == 0) return;

        var data = new Data();
        var idxMap = new Dictionary<Element, int>();

        foreach (var el in selection)
        {
            var copy = DeepClone(el);
            idxMap[el] = data.Elements.Count;
            data.Elements.Add(copy);
            layout.TryGet(el, out var pos);
            data.Positions.Add(new Vector2(pos.X, pos.Y));
        }

        foreach (var w in circuit.Wires)
        {
            var fromOwner = circuit.OwnerOf(w.From);
            var toOwner = circuit.OwnerOf(w.To);
            if (fromOwner is null || toOwner is null) continue;
            if (!idxMap.TryGetValue(fromOwner, out var fi)) continue;
            if (!idxMap.TryGetValue(toOwner, out var ti)) continue;

            var entry = new WireEntry
            {
                FromIdx = fi,
                FromPinIdx = Layout.PinIndex(fromOwner, w.From, isInput: false),
                ToIdx = ti,
                ToPinIdx = Layout.PinIndex(toOwner, w.To, isInput: true)
            };
            foreach (var wp in w.Waypoints) entry.Waypoints.Add(wp);
            data.Wires.Add(entry);
        }

        _clipboard = data;
    }

    public void BeginPaste()
    {
        if (!HasClipboard) return;
        _hologram = _clipboard;
        _center = ComputeBoundingCenter(_clipboard!.Elements, _clipboard.Positions);
    }

    public bool CommitPaste(Circuit circuit, Layout layout, Vector2 atWorld, out List<Element> inserted)
    {
        inserted = new List<Element>();
        if (_hologram is null) return false;

        var data = _hologram;
        var offset = atWorld - _center;

        // Запрет пересечения.
        for (int i = 0; i < data.Elements.Count; i++)
        {
            var el = data.Elements[i];
            var p = data.Positions[i] + offset;
            var r = new Rectangle(p.X, p.Y, el.Width, el.Height);
            foreach (var existing in circuit.Elements)
            {
                if (!layout.TryGet(existing, out var op)) continue;
                if (Raylib.CheckCollisionRecs(r, new Rectangle(op.X, op.Y, existing.Width, existing.Height)))
                    return false;
            }
        }

        var newElements = new List<Element>();
        foreach (var el in data.Elements)
            newElements.Add(DeepClone(el));

        for (int i = 0; i < newElements.Count; i++)
        {
            var p = data.Positions[i] + offset;
            circuit.Add(newElements[i]);
            layout.Place(newElements[i], (int)p.X, (int)p.Y);
        }

        foreach (var cw in data.Wires)
        {
            if (cw.FromIdx < 0 || cw.FromIdx >= newElements.Count) continue;
            if (cw.ToIdx < 0 || cw.ToIdx >= newElements.Count) continue;

            var fromPin = PinByIndex(newElements[cw.FromIdx], cw.FromPinIdx, isInput: false) as OutputPin;
            var toPin = PinByIndex(newElements[cw.ToIdx], cw.ToPinIdx, isInput: true) as InputPin;
            if (fromPin is null || toPin is null) continue;

            var w = circuit.Connect(fromPin, toPin);
            foreach (var wp in cw.Waypoints) w.Waypoints.Add(wp + offset);
        }

        _hologram = null;
        inserted = newElements;
        return true;
    }

    private static Vector2 ComputeBoundingCenter(List<Element> elements, List<Vector2> positions)
    {
        if (positions.Count == 0) return Vector2.Zero;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < elements.Count; i++)
        {
            var p = positions[i];
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X + elements[i].Width);
            maxY = Math.Max(maxY, p.Y + elements[i].Height);
        }
        return new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
    }

    public static Element DeepClone(Element el)
    {
        Element copy = el switch
        {
            InElement => new InElement(),
            OutElement => new OutElement(),
            NandElement => new NandElement(),
            ChipElement ce => ce.Clone(),
            _ => throw new NotSupportedException($"Unknown element: {el.GetType().Name}")
        };

        copy.BodyColor = el.BodyColor;
        copy.WireColor = el.WireColor;

        copy.InputLabels.Clear(); copy.InputLabels.AddRange(el.InputLabels);
        copy.OutputLabels.Clear(); copy.OutputLabels.AddRange(el.OutputLabels);

        for (int i = 0; i < copy.Inputs.Count && i < el.Inputs.Count; i++)
            copy.Inputs[i].Color = el.Inputs[i].Color;
        for (int i = 0; i < copy.Outputs.Count && i < el.Outputs.Count; i++)
            copy.Outputs[i].Color = el.Outputs[i].Color;

        return copy;
    }

    private static Pin? PinByIndex(Element el, int idx, bool isInput)
    {
        if (isInput)
        {
            if (idx >= 0 && idx < el.Inputs.Count) return el.Inputs[idx];
            return null;
        }
        if (idx >= 0 && idx < el.Outputs.Count) return el.Outputs[idx];
        if (el is OutElement oe && idx == 0) return oe.Out;
        return null;
    }
}