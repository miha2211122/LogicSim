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
        ResolveInputs(_elements, _wires);
        foreach (var el in _elements)
            el.Evaluate();
    }

    /// <summary>
    /// Собирает все провода, приходящие на каждый InputPin, и записывает
    /// в пин результат разрешения нескольких драйверов.
    ///
    /// Пины без драйверов НЕ трогаются — это сохраняет пользовательские
    /// переключатели (InElement.In, внешние входы ChipElement), которые
    /// тоже являются InputPin, но не питаются от проводов.
    /// </summary>
    internal static void ResolveInputs(IReadOnlyList<Element> elements, IReadOnlyList<Wire> wires)
    {
        if (wires.Count == 0) return;

        var drivers = new Dictionary<InputPin, LogicState>();
        foreach (var w in wires)
        {
            var s = w.From.State;
            drivers[w.To] = drivers.TryGetValue(w.To, out var prev)
                ? Combine(prev, s)
                : s;
        }

        foreach (var el in elements)
            foreach (var inp in el.Inputs)
                if (drivers.TryGetValue(inp, out var s))
                    inp.SetValue(s);
    }

    /// <summary>
    /// Разрешение двух драйверов на одной линии:
    /// High-Z драйвером не считается, одинаковые уровни — уровень,
    /// разные — Conflict (короткое замыкание).
    /// </summary>
    public static LogicState Combine(LogicState a, LogicState b)
    {
        if (a == b) return a;
        if (a == LogicState.HighZ) return b;
        if (b == LogicState.HighZ) return a;
        return LogicState.Conflict;
    }

    /// <summary>
    /// Итоговое состояние линии на конкретном входе с учётом ВСЕХ
    /// подключённых к нему проводов. Используется для подсказки
    /// при наведении на провод.
    /// </summary>
    public LogicState NetStateAt(InputPin pin)
    {
        LogicState? result = null;
        foreach (var w in _wires)
        {
            if (!ReferenceEquals(w.To, pin)) continue;
            result = result is null ? w.From.State : Combine(result.Value, w.From.State);
        }
        return result ?? LogicState.HighZ;
    }

    /// <summary>
    /// Состояние сети, к которой принадлежит провод.
    /// Это состояние ВХОДА-приёмника: если на один вход идут
    /// два провода с разными уровнями, оба провода — Conflict.
    /// </summary>
    public LogicState NetStateOf(Wire wire) => NetStateAt(wire.To);
}