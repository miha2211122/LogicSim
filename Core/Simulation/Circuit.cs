namespace LogicSim.Game.Core;

public sealed class Circuit
{
    private readonly List<Element> _elements = new();
    private readonly List<Wire> _wires = new();

    public IReadOnlyList<Element> Elements => _elements;
    public IReadOnlyList<Wire> Wires => _wires;
    public List<Wire> WiresList => _wires;

    /// <summary>Пауза останавливает время (CLK/DELAY), но комбинационная логика продолжает считаться.</summary>
    public bool Running { get; set; } = true;
    private bool _stepRequested;

    // Снимок всех пинов: нужен, чтобы отличать «ничего не изменилось» от реального такта.
    private LogicState[] _last = Array.Empty<LogicState>();
    private LogicState[] _current = Array.Empty<LogicState>();
    private LogicState[] _before = Array.Empty<LogicState>();
    private LogicState[] _after = Array.Empty<LogicState>();

    // Топология меняется при подключении/удалении проводов и элементов.
    // Один только снимок пинов это не ловит: новый провод ещё не записал значение.
    private bool _dirty = true;
    private int _wireCount = -1;
    private int _elementCount = -1;

    public void MarkDirty() => _dirty = true;

    public void StepOnce() => _stepRequested = true;

    public T Add<T>(T element) where T : Element
    {
        _elements.Add(element);
        _dirty = true;
        return element;
    }

    public void AddElement(Element el) { _elements.Add(el); _dirty = true; }

    public void Clear()
    {
        var targets = _wires.Select(w => w.To).Distinct().ToArray();
        _elements.Clear();
        _wires.Clear();
        _dirty = true;
        foreach (var pin in targets) pin.ResetBusStates();
    }

    public void Remove(Element element)
    {
        _elements.Remove(element);
        _dirty = true;
        PruneOrphanWires();
    }

    public void RemoveRange(IEnumerable<Element> elements)
    {
        foreach (var el in elements.ToList())
            _elements.Remove(el);

        _dirty = true;
        PruneOrphanWires();
    }

    public void RemoveWire(Wire wire)
    {
        if (!_wires.Remove(wire)) return;
        _dirty = true;
        ClearBusTargetIfDisconnected(wire.To);
    }

    /// <summary>Обновить признак шины у проводов после смены разрядности пинов.</summary>
    public void RefreshBusFlags()
    {
        foreach (var w in _wires)
            w.IsBus = w.From.BitWidth > 1 || w.To.BitWidth > 1;
    }

    private void PruneOrphanWires()
    {
        var orphaned = _wires
            .Where(w => OwnerOf(w.From) is null || OwnerOf(w.To) is null)
            .ToList();
        if (orphaned.Count == 0) return;

        _wires.RemoveAll(w => orphaned.Contains(w));
        _dirty = true;
        foreach (var w in orphaned)
            ClearBusTargetIfDisconnected(w.To);
    }

    /// <summary>Чистим биты входа только если к нему не осталось других шинных проводов.</summary>
    private void ClearBusTargetIfDisconnected(InputPin pin)
    {
        if (!_wires.Any(w => w.IsBus && ReferenceEquals(w.To, pin)))
            pin.ResetBusStates();

        // Одиночный провод больше не подключён — сбрасываем старое значение,
        // чтобы вход не оставался залипшим после удаления провода.
        // Входы IN-элементов — это пользовательские переключатели, их не трогаем.
        if (!_wires.Any(w => ReferenceEquals(w.To, pin)) &&
            OwnerOf(pin) is not InElement)
            pin.SetValue(LogicState.HighZ);
    }

    public bool ContainsWire(OutputPin from, InputPin to) =>
        _wires.Any(w => ReferenceEquals(w.From, from) && ReferenceEquals(w.To, to));

    public Wire Connect(OutputPin from, InputPin to)
    {
        var w = new Wire(from, to);
        _wires.Add(w);
        _dirty = true;
        return w;
    }

    public Wire AddWire(OutputPin from, InputPin to) => Connect(from, to);

    public Element? OwnerOf(Pin pin)
    {
        foreach (var el in _elements)
        {
            foreach (var p in el.Inputs) if (ReferenceEquals(p, pin)) return el;
            foreach (var p in el.Outputs) if (ReferenceEquals(p, pin)) return el;
        }
        return null;
    }

    public void Step()
    {
        bool tick = Running || _stepRequested;
        _stepRequested = false;

        bool topoChanged = _dirty
            || _elements.Count != _elementCount
            || _wires.Count != _wireCount;
        _dirty = false;
        _elementCount = _elements.Count;
        _wireCount = _wires.Count;

        // Внешние изменения (клик по IN, провода, загрузка) видны по снимку пинов.
        _current = Capture(_current);
        bool changed = topoChanged || !Same(_current, _last);

        if (tick)
        {
            foreach (var el in _elements)
                if (el is ITickable tickable)
                    tickable.Advance();

            _current = Capture(_current);
            changed |= !Same(_current, _last);
        }

        // Пустой такт: время не изменило выходы и входы не трогали — не считаем ничего.
        if (!changed) return;

        Recompute();
    }

    /// <summary>
    /// Пересчитать цепь без тактирования. Вызывается при подключении проводов
    /// и для внешних контекстов, пока пользователь редактирует вложенный макрос.
    /// </summary>
    public void Settle() => Recompute();

    private void Recompute()
    {
        // Один такт = все вычисления: крутим до тех пор, пока состояние не перестанет меняться.
        int maxPasses = Math.Max(2, _elements.Count + 2);
        for (int p = 0; p < maxPasses; p++)
        {
            _before = Capture(_before);
            ResolveInputs(_elements, _wires);
            foreach (var el in _elements)
                el.Evaluate();
            _after = Capture(_after);

            if (Same(_before, _after)) break;
        }

        (_last, _after) = (_after, _last);
    }

    private LogicState[] Capture(LogicState[] target)
    {
        int count = 0;
        foreach (var el in _elements)
            count += (el.Inputs.Count + el.Outputs.Count) * 33;

        if (target.Length != count) target = new LogicState[count];

        int idx = 0;
        foreach (var el in _elements)
        {
            foreach (var pin in el.Inputs) idx = WritePin(pin, target, idx);
            foreach (var pin in el.Outputs) idx = WritePin(pin, target, idx);
        }
        return target;
    }

    private static int WritePin(Pin pin, LogicState[] dst, int idx)
    {
        dst[idx++] = pin.State;
        for (int i = 0; i < 32; i++) dst[idx++] = pin.BusStates[i];
        return idx;
    }

    private static bool Same(LogicState[] a, LogicState[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    [ThreadStatic] private static NetSolver? _solver;

    internal static void ResolveInputs(IReadOnlyList<Element> elements, IReadOnlyList<Wire> wires)
        => (_solver ??= new NetSolver()).Resolve(elements, wires);

    internal static LogicState Aggregate(LogicState[] bits)
    {
        bool anyConflict = false, anyOne = false, anyDriven = false;
        for (int i = 0; i < 32; i++)
        {
            var s = bits[i];
            if (s == LogicState.Conflict) anyConflict = true;
            else if (s == LogicState.One) anyOne = true;
            if (s != LogicState.HighZ) anyDriven = true;
        }
        if (anyConflict) return LogicState.Conflict;
        if (anyOne) return LogicState.One;
        return anyDriven ? LogicState.Zero : LogicState.HighZ;
    }

    /// <summary>Подключённый провод всегда считается определённым: Z — только при полном отсутствии проводов.</summary>
    internal static LogicState Driven(LogicState s)
        => s == LogicState.HighZ ? LogicState.Zero : s;

    public static LogicState Combine(LogicState a, LogicState b)
    {
        if (a == b) return a;
        if (a == LogicState.HighZ) return b;
        if (b == LogicState.HighZ) return a;
        return LogicState.Conflict;
    }

    public LogicState NetStateAt(InputPin pin)
    {
        LogicState? strong = null, weak = null;
        foreach (var w in _wires)
        {
            if (!ReferenceEquals(w.To, pin)) continue;
            if (w.From.Weak)
                weak = weak is null ? w.From.State : Combine(weak.Value, w.From.State);
            else
                strong = strong is null ? w.From.State : Combine(strong.Value, w.From.State);
        }
        return strong ?? weak ?? LogicState.HighZ;
    }

    public LogicState NetStateOf(Wire wire) => NetStateAt(wire.To);
}
/// <summary>
/// Переиспользуемые буферы для ResolveInputs. Симуляция вызывает решатель по несколько
/// раз за такт (и отдельно для каждого вложенного макроса), поэтому выделять словари
/// и 32-битные массивы шин заново на каждом проходе — лишняя работа для GC.
/// </summary>
internal sealed class NetSolver
{
    private const int MaxCachedBuses = 4096;

    private sealed class BusBuffers
    {
        public readonly Dictionary<InputPin, LogicState[]> Arrays = new();
        public readonly List<LogicState[]> Used = new();

        public LogicState[] Rent(InputPin pin)
        {
            if (!Arrays.TryGetValue(pin, out var bits))
            {
                bits = new LogicState[32];
                Array.Fill(bits, LogicState.HighZ);
                Arrays.Add(pin, bits);
            }
            Used.Add(bits);
            return bits;
        }

        public void Reset()
        {
            foreach (var bits in Used)
                Array.Fill(bits, LogicState.HighZ);
            Used.Clear();
        }

        /// <summary>Не держим массивы пинов из удалённых/пересозданных схем вечно.</summary>
        public void Trim()
        {
            if (Arrays.Count > MaxCachedBuses)
                Arrays.Clear();
        }
    }

    private readonly Dictionary<InputPin, LogicState> _drivers = new();
    private readonly Dictionary<InputPin, LogicState> _weakDrivers = new();
    private readonly Dictionary<InputPin, LogicState[]> _busBits = new();
    private readonly Dictionary<InputPin, LogicState[]> _weakBusBits = new();
    private readonly Dictionary<InputPin, LogicState[]> _weakBusBase = new();
    private readonly BusBuffers _strongBuffers = new();
    private readonly BusBuffers _weakBuffers = new();
    private readonly BusBuffers _baseBuffers = new();

    public void Resolve(IReadOnlyList<Element> elements, IReadOnlyList<Wire> wires)
    {
        _drivers.Clear();
        _weakDrivers.Clear();
        _busBits.Clear();
        _weakBusBits.Clear();
        _weakBusBase.Clear();
        _strongBuffers.Reset();
        _weakBuffers.Reset();
        _baseBuffers.Reset();
        _strongBuffers.Trim();
        _weakBuffers.Trim();
        _baseBuffers.Trim();

        // Пин считается подключённым, если в него приходит валидный провод.
        // Эти флаги нужны элементам, чтобы не выдавать Z при частично
        // подключённых входах.
        foreach (var el in elements)
            foreach (var inp in el.Inputs) inp.IsWired = false;
        foreach (var w in wires)
            if (w.IsValid) w.To.IsWired = true;

        if (wires.Count == 0) return;

        foreach (var w in wires)
        {
            if (!w.IsValid) continue;

            bool strong = !w.From.Weak;
            var scalar = strong ? _drivers : _weakDrivers;
            var bus = strong ? _busBits : _weakBusBits;
            var buffers = strong ? _strongBuffers : _weakBuffers;

            if (w.IsBus)
            {
                if (!bus.TryGetValue(w.To, out var bits))
                {
                    // Сохраняем то, что было на пине до слабой шины: при сильном
                    // скалярном драйвере слабые биты должны исчезнуть, но не
                    // затерть состояние, внедрённое родительским чипом.
                    if (ReferenceEquals(buffers, _weakBuffers))
                    {
                        var original = _baseBuffers.Rent(w.To);
                        Array.Copy(w.To.BusStates, original, 32);
                        _weakBusBase[w.To] = original;
                    }
                    bits = buffers.Rent(w.To);
                    bus[w.To] = bits;
                }
                for (int i = 0; i < 32; i++)
                    bits[i] = Circuit.Combine(bits[i], Circuit.Driven(w.From.BusStates[i]));
            }
            else
            {
                var s = Circuit.Driven(w.From.State);
                scalar[w.To] = scalar.TryGetValue(w.To, out var prev)
                    ? Circuit.Combine(prev, s)
                    : s;
            }
        }

        foreach (var (pin, bits) in _busBits)
        {
            // Обычный провод на тот же пин влияет на младший бит шины.
            if (_drivers.TryGetValue(pin, out var scalar))
            {
                bits[0] = Circuit.Combine(bits[0], scalar);
                _drivers.Remove(pin);
            }

            // Сильная шина перекрывает все слабые источники на этом пине.
            _weakBusBits.Remove(pin);
            _weakDrivers.Remove(pin);

            for (int i = 0; i < 32; i++)
                pin.BusStates[i] = bits[i];
            pin.SetValue(Circuit.Aggregate(bits));
        }

        foreach (var (pin, bits) in _weakBusBits)
        {
            if (_busBits.ContainsKey(pin)) continue;
            if (_drivers.ContainsKey(pin))
            {
                if (_weakBusBase.TryGetValue(pin, out var original))
                {
                    for (int i = 0; i < 32; i++)
                        pin.BusStates[i] = original[i];
                }
                else pin.ResetBusStates();
                continue;
            }
            if (_weakDrivers.TryGetValue(pin, out var scalar))
            {
                bits[0] = Circuit.Combine(bits[0], scalar);
                _weakDrivers.Remove(pin);
            }

            for (int i = 0; i < 32; i++)
                pin.BusStates[i] = bits[i];
            pin.SetValue(Circuit.Aggregate(bits));
        }

        foreach (var el in elements)
            foreach (var inp in el.Inputs)
            {
                if (_drivers.TryGetValue(inp, out var s)) inp.SetValue(s);
                else if (_weakDrivers.TryGetValue(inp, out var weak)) inp.SetValue(weak);
            }
    }
}
