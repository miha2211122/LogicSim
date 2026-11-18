using System.Drawing;
using SDColor = System.Drawing.Color;

namespace LogicSim.Game.Core;

public abstract class Element
{
    public const int DefaultWidth = 130;
    public const int DefaultHeight = 80;

    public abstract string Name { get; }
    public abstract IReadOnlyList<InputPin> Inputs { get; }
    public abstract IReadOnlyList<OutputPin> Outputs { get; }
    public abstract void Evaluate();

    public virtual int Width => DefaultWidth;
    public virtual int Height => DefaultHeight;

    public SDColor BodyColor { get; set; } = SDColor.FromArgb(45, 50, 70);
    public SDColor WireColor { get; set; } = SDColor.FromArgb(80, 220, 80);

    /// <summary>Номер слоя, к которому принадлежит элемент.</summary>
    public int Layer { get; set; }

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
    protected static void Set(OutputPin pin, LogicState state) => pin.SetValue(state);
}

public sealed class InElement : Element
{
    public override string Name => "IN";
    public int BitWidth { get; private set; } = 1;

    public InputPin[] In { get; private set; } = Array.Empty<InputPin>();
    public OutputPin Out { get; } = new();

    public override IReadOnlyList<InputPin> Inputs => In;
    public override IReadOnlyList<OutputPin> Outputs => new[] { Out };

    public override int Height => Math.Max(80, BitWidth * 24 + 20);
    public override int Width => 90;

    public InElement(int bitWidth = 1)
    {
        SetBitWidth(bitWidth);
    }

    public void SetBitWidth(int width)
    {
        BitWidth = Math.Clamp(width, 1, 32);
        In = Enumerable.Range(0, BitWidth).Select(_ => new InputPin()).ToArray();
        Out.BitWidth = BitWidth;

        foreach (var pin in In) pin.SetValue(LogicState.Zero);
        Out.SetValue(LogicState.Zero);
    }

    public override void Evaluate()
    {
        if (BitWidth == 1)
        {
            Set(Out, In[0].State);
        }
        else
        {
            // Если внешняя шина НЕ подключена к 0-му пину, берем данные с переключателей
            bool isDrivenByBus = In[0].BusStates.Any(s => s != LogicState.HighZ);

            if (isDrivenByBus)
            {
                for (int i = 0; i < BitWidth; i++) Out.BusStates[i] = In[0].BusStates[i];
            }
            else
            {
                for (int i = 0; i < BitWidth; i++) Out.BusStates[i] = In[i].State;
            }

            bool anyOne = false;
            for (int i = 0; i < BitWidth; i++)
                if (Out.BusStates[i] == LogicState.One) anyOne = true;

            Set(Out, anyOne ? LogicState.One : LogicState.Zero);
        }
    }

    public void Toggle()
    {
        if (BitWidth == 1)
        {
            In[0].Toggle();
        }
    }
}

public sealed class OutElement : Element
{
    public override string Name => "OUT";
    public int BitWidth { get; private set; } = 1;

    public InputPin In { get; } = new();
    public OutputPin[] InternalOut { get; private set; } = Array.Empty<OutputPin>();

    // Алиас для обратной совместимости (первый скрытый пин)
    public OutputPin Out => InternalOut[0];

    public override IReadOnlyList<InputPin> Inputs => new[] { In };
    public override IReadOnlyList<OutputPin> Outputs => Array.Empty<OutputPin>();

    public override int Height => 80;
    public override int Width => 90;

    public OutElement(int bitWidth = 1)
    {
        SetBitWidth(bitWidth);
    }

    public void SetBitWidth(int width)
    {
        BitWidth = Math.Clamp(width, 1, 32);
        InternalOut = Enumerable.Range(0, BitWidth).Select(_ => new OutputPin()).ToArray();
        In.BitWidth = BitWidth;
    }

    public override void Evaluate()
    {
        if (BitWidth == 1)
        {
            Set(InternalOut[0], In.State);
        }
        else
        {
            for (int i = 0; i < BitWidth; i++)
                InternalOut[i].SetValue(In.BusStates[i]);
        }
    }
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

    public override void Evaluate()
    {
        var a = A.State;
        var b = B.State;

        if (a == LogicState.Zero || b == LogicState.Zero) { Set(Out, LogicState.One); return; }
        if (a == LogicState.Conflict || b == LogicState.Conflict) { Set(Out, LogicState.Conflict); return; }
        if (a == LogicState.HighZ || b == LogicState.HighZ) { Set(Out, LogicState.HighZ); return; }
        Set(Out, LogicState.Zero);
    }
}

public sealed class BusMerger : Element
{
    public override string Name => "MERGE";
    public int BitWidth { get; private set; } = 8;

    public InputPin[] In { get; private set; } = Array.Empty<InputPin>();
    public OutputPin Out { get; } = new();

    public override IReadOnlyList<InputPin> Inputs => In;
    public override IReadOnlyList<OutputPin> Outputs => new[] { Out };

    public override int Height => Math.Max(80, BitWidth * 24 + 20);
    public override int Width => 90;

    public BusMerger(int bitWidth = 8) => SetBitWidth(bitWidth);

    public void SetBitWidth(int width)
    {
        BitWidth = Math.Clamp(width, 2, 32);
        In = Enumerable.Range(0, BitWidth).Select(_ => new InputPin()).ToArray();

        InputLabels.Clear();
        for (int i = 0; i < BitWidth; i++) InputLabels.Add((i + 1).ToString());

        OutputLabels.Clear();
        OutputLabels.Add("Bus");
        Out.BitWidth = BitWidth;
    }

    public override void Evaluate()
    {
        bool anyConflict = false;
        bool anyOne = false;

        for (int i = 0; i < BitWidth; i++)
        {
            var state = In[i].State;
            if (state == LogicState.HighZ) state = LogicState.Zero;

            Out.BusStates[i] = state;

            if (state == LogicState.Conflict) anyConflict = true;
            if (state == LogicState.One) anyOne = true;
        }

        if (anyConflict) Set(Out, LogicState.Conflict);
        else if (anyOne) Set(Out, LogicState.One);
        else Set(Out, LogicState.Zero);
    }
}

public sealed class BusSplitter : Element
{
    public override string Name => "SPLIT";
    public int BitWidth { get; private set; } = 8;

    public InputPin In { get; } = new();
    public OutputPin[] Out { get; private set; } = Array.Empty<OutputPin>();

    public override IReadOnlyList<InputPin> Inputs => new[] { In };
    public override IReadOnlyList<OutputPin> Outputs => Out;

    public override int Height => Math.Max(80, BitWidth * 24 + 20);
    public override int Width => 90;

    public BusSplitter(int bitWidth = 8) => SetBitWidth(bitWidth);

    public void SetBitWidth(int width)
    {
        BitWidth = Math.Clamp(width, 2, 32);
        Out = Enumerable.Range(0, BitWidth).Select(_ => new OutputPin()).ToArray();

        InputLabels.Clear();
        InputLabels.Add("Bus");

        OutputLabels.Clear();
        for (int i = 0; i < BitWidth; i++) OutputLabels.Add((i + 1).ToString());
        In.BitWidth = BitWidth;
    }

    public override void Evaluate()
    {
        if (In.State == LogicState.HighZ)
        {
            foreach (var o in Out) Set(o, LogicState.HighZ);
        }
        else
        {
            for (int i = 0; i < BitWidth; i++)
                Out[i].SetValue(In.BusStates[i]);
        }
    }
}

public sealed class ChipElement : Element
{
    public const int MinWidth = 80;
    public const int MinHeight = 50;
    public const int MinPinSpacing = 18;

    private readonly List<Element> _inner;
    private readonly List<Wire> _innerWires;
    private readonly (InputPin ext, InputPin inner)[] _inputs;
    private readonly (InputPin inner, OutputPin ext)[] _outputs;
    private readonly Dictionary<Element, (int X, int Y)> _innerPositions = new();
    private readonly Dictionary<Pin, float> _pinOffsets = new();

    private string _name;
    private int _width = DefaultWidth;
    private int _height = DefaultHeight;

    public override int Width => _width;
    public override int Height => _height;

    public override string Name => _name;
    public void Rename(string name) { if (!string.IsNullOrEmpty(name)) _name = name; }

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
        (InputPin inner, OutputPin ext)[] outputs)
    {
        _name = name;
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

        ApplyDefaultOffsets();
    }

    private void ApplyDefaultOffsets()
    {
        for (int i = 0; i < Inputs.Count; i++)
            _pinOffsets[Inputs[i]] = DefaultOffset(Inputs.Count, i);
        for (int i = 0; i < Outputs.Count; i++)
            _pinOffsets[Outputs[i]] = DefaultOffset(Outputs.Count, i);
    }

    public static float DefaultOffset(int count, int index)
        => count == 1 ? 0.5f : (index + 1f) / (count + 1f);

    public IReadOnlyList<Element> Inner => _inner;
    public List<Wire> InnerWires => _innerWires;
    public IReadOnlyDictionary<Element, (int X, int Y)> InnerPositions => _innerPositions;

    /// <summary>Слои внутреннего контекста микросхемы.</summary>
    public LayerSet LayerState { get; } = new();

    public void SetInnerPosition(Element el, int x, int y) => _innerPositions[el] = (x, y);

    public void SyncInner(IReadOnlyList<Element> elements, IReadOnlyList<Wire> wires)
    {
        _inner.Clear();
        _inner.AddRange(elements);

        _innerWires.Clear();
        _innerWires.AddRange(wires);

        foreach (var el in _innerPositions.Keys.ToList())
            if (!_inner.Contains(el))
                _innerPositions.Remove(el);
    }

    public void RemapExternalPins()
    {
        var ins = _inner.OfType<InElement>().ToList();
        for (int i = 0; i < _inputs.Length && i < ins.Count; i++)
            _inputs[i] = (_inputs[i].ext, ins[i].In[0]);

        var outs = _inner.OfType<OutElement>().ToList();
        for (int i = 0; i < _outputs.Length && i < outs.Count; i++)
            _outputs[i] = (outs[i].In, _outputs[i].ext);
    }

    public void SetSize(int w, int h)
    {
        int maxPins = Math.Max(Inputs.Count, Outputs.Count);
        int minH = Math.Max(MinHeight, maxPins * MinPinSpacing);
        _width = Math.Max(MinWidth, w);
        _height = Math.Max(minH, h);
    }

    public float GetOffset(Pin pin)
        => _pinOffsets.TryGetValue(pin, out var v) ? v : 0.5f;

    public void SetOffset(Pin pin, float value)
    {
        if (value < 0.08f) value = 0.08f;
        if (value > 0.92f) value = 0.92f;
        _pinOffsets[pin] = value;
    }

    public void ResyncExternalLabels()
    {
        for (int i = 0; i < _inputs.Length; i++)
        {
            var inner = _inputs[i].inner;
            foreach (var el in _inner)
            {
                if (el is InElement ie && ie.In.Contains(inner))
                {
                    if (ie.OutputLabels.Count > 0 && i < InputLabels.Count)
                        InputLabels[i] = ie.OutputLabels[0];
                    break;
                }
            }
        }
        for (int i = 0; i < _outputs.Length; i++)
        {
            var inner = _outputs[i].inner;
            foreach (var el in _inner)
            {
                if (el is OutElement oe && ReferenceEquals(oe.In, inner))
                {
                    if (oe.InputLabels.Count > 0 && i < OutputLabels.Count)
                        OutputLabels[i] = oe.InputLabels[0];
                    break;
                }
            }
        }
    }

    public override void Evaluate()
    {
        foreach (var (ext, inner) in _inputs)
        {
            if (ext.BitWidth > 1)
            {
                for (int i = 0; i < 32; i++) inner.BusStates[i] = ext.BusStates[i];
            }
            inner.SetValue(ext.State);
        }

        int passes = _inner.Count + 2;
        for (int p = 0; p < passes; p++)
        {
            Circuit.ResolveInputs(_inner, _innerWires);
            foreach (var el in _inner) el.Evaluate();
        }

        foreach (var (inner, ext) in _outputs)
        {
            if (ext.BitWidth > 1)
            {
                for (int i = 0; i < 32; i++) ext.BusStates[i] = inner.BusStates[i];
            }
            ext.SetValue(inner.State);
        }
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
            nw.IsBus = w.IsBus;
            nw.Layer = w.Layer;
            newWires.Add(nw);
        }

        var newInputs = new List<(InputPin ext, InputPin inner)>();
        foreach (var (_, inner) in _inputs)
        {
            var owner = FindOwner(inner);
            if (owner is null) continue;
            var newExt = new InputPin { BitWidth = inner.BitWidth };
            newInputs.Add((newExt, (InputPin)MapPin(owner, inner, map[owner])));
        }

        var newOutputs = new List<(InputPin inner, OutputPin ext)>();
        foreach (var (inner, _) in _outputs)
        {
            var owner = FindOwner(inner);
            if (owner is null) continue;
            var newExt = new OutputPin { BitWidth = inner.BitWidth };
            newOutputs.Add(((InputPin)MapPin(owner, inner, map[owner]), newExt));
        }

        var chip = new ChipElement(_name, newInner.ToArray(), newWires.ToArray(),
                                   newInputs.ToArray(), newOutputs.ToArray());
        chip.BodyColor = BodyColor;
        chip.WireColor = WireColor;
        chip.LayerState.CopyFrom(LayerState);
        chip.InputLabels.Clear(); chip.InputLabels.AddRange(InputLabels);
        chip.OutputLabels.Clear(); chip.OutputLabels.AddRange(OutputLabels);
        chip.SetSize(_width, _height);

        for (int i = 0; i < Inputs.Count && i < chip.Inputs.Count; i++)
        {
            chip.SetOffset(chip.Inputs[i], GetOffset(Inputs[i]));
            chip.Inputs[i].Color = Inputs[i].Color;
        }
        for (int i = 0; i < Outputs.Count && i < chip.Outputs.Count; i++)
        {
            chip.SetOffset(chip.Outputs[i], GetOffset(Outputs[i]));
            chip.Outputs[i].Color = Outputs[i].Color;
        }

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
        }
        return null;
    }

    private static Pin MapPin(Element oldEl, Pin oldPin, Element newEl)
    {
        for (int i = 0; i < oldEl.Inputs.Count; i++)
            if (ReferenceEquals(oldEl.Inputs[i], oldPin)) return newEl.Inputs[i];
        for (int i = 0; i < oldEl.Outputs.Count; i++)
            if (ReferenceEquals(oldEl.Outputs[i], oldPin)) return newEl.Outputs[i];
        throw new InvalidOperationException("Pin not found during cloning.");
    }

    private static Element CloneElement(Element el)
    {
        Element copy = el switch
        {
            NandElement => new NandElement(),
            InElement ie => new InElement(ie.BitWidth),
            OutElement oe => new OutElement(oe.BitWidth),
            BusMerger bm => new BusMerger(bm.BitWidth),
            BusSplitter bs => new BusSplitter(bs.BitWidth),
            ChipElement ce => ce.Clone(),
            _ => new NandElement()
        };

        copy.BodyColor = el.BodyColor;
        copy.WireColor = el.WireColor;

        copy.InputLabels.Clear();
        copy.InputLabels.AddRange(el.InputLabels);

        copy.OutputLabels.Clear();
        copy.OutputLabels.AddRange(el.OutputLabels);

        for (int i = 0; i < copy.Inputs.Count && i < el.Inputs.Count; i++)
            copy.Inputs[i].Color = el.Inputs[i].Color;

        for (int i = 0; i < copy.Outputs.Count && i < el.Outputs.Count; i++)
            copy.Outputs[i].Color = el.Outputs[i].Color;

        return copy;
    }
}