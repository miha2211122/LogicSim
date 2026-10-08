namespace LogicSim.Game.Core;

public sealed class Circuit
{
    private readonly List<Element> _elements = new();
    private readonly List<Wire> _wires = new();

    public IReadOnlyList<Element> Elements => _elements;
    public IReadOnlyList<Wire> Wires => _wires;
    public List<Wire> WiresList => _wires;

    public T Add<T>(T element) where T : Element
    {
        _elements.Add(element);
        return element;
    }

    public void AddElement(Element el) => _elements.Add(el);
    public void Clear() { _elements.Clear(); _wires.Clear(); }

    public void Remove(Element element)
    {
        _elements.Remove(element);
        _wires.RemoveAll(w =>
            !_elements.Any(e => e.Outputs.Contains(w.From)) ||
            !_elements.Any(e => e.Inputs.Contains(w.To)));
    }

    public void RemoveRange(IEnumerable<Element> elements)
    {
        foreach (var el in elements.ToList())
            _elements.Remove(el);

        _wires.RemoveAll(w =>
            !_elements.Any(e => e.Outputs.Contains(w.From)) ||
            !_elements.Any(e => e.Inputs.Contains(w.To)));
    }

    public void RemoveWire(Wire wire) => _wires.Remove(wire);

    public Wire Connect(OutputPin from, InputPin to)
    {
        var w = new Wire(from, to);
        _wires.Add(w);
        return w;
    }

    public Wire AddWire(OutputPin from, InputPin to) => Connect(from, to);

    public Element? OwnerOf(Pin pin)
    {
        foreach (var el in _elements)
        {
            foreach (var p in el.Inputs) if (ReferenceEquals(p, pin)) return el;
            foreach (var p in el.Outputs) if (ReferenceEquals(p, pin)) return el;
            if (el is OutElement oe && ReferenceEquals(oe.Out, pin)) return el;
        }
        return null;
    }

    public void Step()
    {
        foreach (var w in _wires)
            w.To.SetValue(w.From.Value);

        foreach (var el in _elements)
            el.Evaluate();
    }
}