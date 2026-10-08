using System.Drawing;

namespace LogicSim.Game.Core;

public abstract class Element
{
    public abstract string Name { get; }
    public abstract IReadOnlyList<InputPin> Inputs { get; }
    public abstract IReadOnlyList<OutputPin> Outputs { get; }
    public abstract void Evaluate();

    public Color BodyColor { get; set; } = Color.FromArgb(45, 50, 70);
    public Color WireColor { get; set; } = Color.FromArgb(80, 220, 80);

    private List<string>? _inputLabels;
    private List<string>? _outputLabels;

    public List<string> InputLabels => _inputLabels ??= DefaultInputLabels().ToList();
    public List<string> OutputLabels => _outputLabels ??= DefaultOutputLabels().ToList();

    protected virtual IEnumerable<string> DefaultInputLabels()
        => Enumerable.Range(1, Inputs.Count).Select(i => i.ToString());

    protected virtual IEnumerable<string> DefaultOutputLabels()
        => Enumerable.Range(1, Outputs.Count).Select(i => i.ToString());

    public void ResetPinLabels() { _inputLabels = null; _outputLabels = null; }
    protected static void Set(OutputPin pin, bool value) => pin.SetValue(value);
}

public sealed class InElement : Element
{
    public override string Name => "IN";
    public InputPin In { get; } = new();
    public OutputPin Out { get; } = new();
    public override IReadOnlyList<InputPin> Inputs => new[] { In };
    public override IReadOnlyList<OutputPin> Outputs => new[] { Out };
    public override void Evaluate() => Set(Out, In.Value);
}

public sealed class OutElement : Element
{
    public override string Name => "OUT";
    public InputPin In { get; } = new();
    public OutputPin Out { get; } = new();
    public override IReadOnlyList<InputPin> Inputs => new[] { In };
    public override IReadOnlyList<OutputPin> Outputs => Array.Empty<OutputPin>();
    public override void Evaluate() => Set(Out, In.Value);
}

public sealed class NandElement : Element
{
    public override string Name => "NAND";
    public InputPin A { get; } = new();
    public InputPin B { get; } = new();
    public OutputPin Out { get; } = new();

    public override IReadOnlyList<InputPin> Inputs => new[] { A, B };
    public override IReadOnlyList<OutputPin> Outputs => new[] { Out };

    protected override IEnumerable<string> DefaultInputLabels() => new[] { "A", "B" };
    protected override IEnumerable<string> DefaultOutputLabels() => new[] { "Y" };

    public override void Evaluate() => Set(Out, !(A.Value && B.Value));
}

public sealed class ChipElement : Element
{
    private readonly List<Element> _inner;
    private readonly List<Wire> _innerWires;
    private readonly (InputPin ext, InputPin inner)[] _inputs;
    private readonly (OutputPin inner, OutputPin ext)[] _outputs;
    private readonly Dictionary<Element, (int X, int Y)> _innerPositions = new();

    public override string Name { get; }
    public override IReadOnlyList<InputPin> Inputs { get; }
    public override IReadOnlyList<OutputPin> Outputs { get; }

    protected override IEnumerable<string> DefaultInputLabels()
        => Enumerable.Range(1, Inputs.Count).Select(i => $"IN{i}");
    protected override IEnumerable<string> DefaultOutputLabels()
        => Enumerable.Range(1, Outputs.Count).Select(i => $"O{i}");

    public ChipElement(
        string name,
        Element[] inner,
        Wire[] innerWires,
        (InputPin ext, InputPin inner)[] inputs,
        (OutputPin inner, OutputPin ext)[] outputs)
    {
        Name = name;
        _inner = inner.ToList();
        _innerWires = innerWires.ToList();
        _inputs = inputs;
        _outputs = outputs;
        Inputs = inputs.Select(x => x.ext).ToArray();
        Outputs = outputs.Select(x => x.ext).ToArray();

        int i = 0;
        foreach (var el in _inner)
        {
            _innerPositions[el] = (60 + (i % 3) * 200, 60 + (i / 3) * 140);
            i++;
        }
    }

    public IReadOnlyList<Element> Inner => _inner;
    public List<Wire> InnerWires => _innerWires;
    public IReadOnlyDictionary<Element, (int X, int Y)> InnerPositions => _innerPositions;

    public void SetInnerPosition(Element el, int x, int y) => _innerPositions[el] = (x, y);

    public override void Evaluate()
    {
        foreach (var (ext, inner) in _inputs)
            inner.SetValue(ext.Value);

        int passes = _inner.Count + 2;
        for (int p = 0; p < passes; p++)
        {
            foreach (var w in _innerWires) w.To.SetValue(w.From.Value);
            foreach (var el in _inner) el.Evaluate();
        }

        foreach (var (inner, ext) in _outputs)
            ext.SetValue(inner.Value);
    }

    public ChipElement Clone()
    {
        var newInner = new List<Element>();
        var map = new Dictionary<Element, Element>();

        foreach (var el in _inner)
        {
            var copy = CloneElement(el);
            newInner.Add(copy);
            map[el] = copy;
        }

        var newWires = new List<Wire>();
        foreach (var w in _innerWires)
        {
            var fo = FindOwner(w.From);
            var to = FindOwner(w.To);
            if (fo is null || to is null) continue;
            var nw = new Wire(
                (OutputPin)MapPin(fo, w.From, map[fo]),
                (InputPin)MapPin(to, w.To, map[to]));
            foreach (var wp in w.Waypoints) nw.Waypoints.Add(wp);
            newWires.Add(nw);
        }

        var newInputs = new List<(InputPin ext, InputPin inner)>();
        foreach (var (_, inner) in _inputs)
        {
            var owner = FindOwner(inner);
            if (owner is null) continue;
            newInputs.Add((new InputPin(), (InputPin)MapPin(owner, inner, map[owner])));
        }

        var newOutputs = new List<(OutputPin inner, OutputPin ext)>();
        foreach (var (inner, _) in _outputs)
        {
            var owner = FindOwner(inner);
            if (owner is null) continue;
            newOutputs.Add(((OutputPin)MapPin(owner, inner, map[owner]), new OutputPin()));
        }

        var chip = new ChipElement(Name, newInner.ToArray(), newWires.ToArray(),
                                   newInputs.ToArray(), newOutputs.ToArray());
        chip.BodyColor = BodyColor;
        chip.WireColor = WireColor;
        chip.InputLabels.Clear(); chip.InputLabels.AddRange(InputLabels);
        chip.OutputLabels.Clear(); chip.OutputLabels.AddRange(OutputLabels);

        foreach (var kv in _innerPositions)
            if (map.TryGetValue(kv.Key, out var newEl))
                chip.SetInnerPosition(newEl, kv.Value.X, kv.Value.Y);

        return chip;
    }

    private Element? FindOwner(Pin pin)
    {
        foreach (var el in _inner)
        {
            foreach (var p in el.Inputs) if (ReferenceEquals(p, pin)) return el;
            foreach (var p in el.Outputs) if (ReferenceEquals(p, pin)) return el;
            if (el is OutElement oe && ReferenceEquals(oe.Out, pin)) return el;
        }
        return null;
    }

    private static Pin MapPin(Element oldEl, Pin oldPin, Element newEl)
    {
        for (int i = 0; i < oldEl.Inputs.Count; i++)
            if (ReferenceEquals(oldEl.Inputs[i], oldPin)) return newEl.Inputs[i];
        for (int i = 0; i < oldEl.Outputs.Count; i++)
            if (ReferenceEquals(oldEl.Outputs[i], oldPin)) return newEl.Outputs[i];
        if (oldEl is OutElement oo && newEl is OutElement no && ReferenceEquals(oo.Out, oldPin))
            return no.Out;
        throw new InvalidOperationException("Pin not found during cloning.");
    }

    private static Element CloneElement(Element el) => el switch
    {
        NandElement => new NandElement(),
        InElement => new InElement(),
        OutElement => new OutElement(),
        ChipElement ce => ce.Clone(),
        _ => throw new NotSupportedException($"Unknown element: {el.GetType().Name}")
    };
}