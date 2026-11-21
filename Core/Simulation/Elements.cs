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

    /// <summary>Подгоняет список подписей под новую разрядность, сохраняя пользовательские имена.</summary>
    protected static void ResizeLabels(List<string> labels, int count, Func<int, string> defaultName)
    {
        while (labels.Count < count) labels.Add(defaultName(labels.Count));
        if (labels.Count > count) labels.RemoveRange(count, labels.Count - count);
    }

    protected static void Set(OutputPin pin, bool value) => pin.SetValue(value);
    protected static void Set(OutputPin pin, LogicState state) => pin.SetValue(state);

    /// <summary>Есть ли у элемента хоть один вход с проводом.</summary>
    protected bool HasInputWire => Inputs.Any(p => p.IsWired);

    /// <summary>
    /// Подключённый вход не бывает Z: если провод есть, а источник не выдаёт
    /// определение — читаем как 0. Z остаётся только у полностью отключённого элемента.
    /// </summary>
    protected LogicState Read(InputPin pin)
        => pin.State == LogicState.HighZ && HasInputWire ? LogicState.Zero : pin.State;

    protected LogicState ReadBusBit(InputPin pin, int bit)
    {
        var s = pin.BusStates[bit];
        return s == LogicState.HighZ && HasInputWire ? LogicState.Zero : s;
    }
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
        Out.ResetBusStates();
        ResizeLabels(InputLabels, BitWidth, i => (i + 1).ToString());
        ResizeLabels(OutputLabels, 1, _ => "1");

        foreach (var pin in In) pin.SetValue(LogicState.Zero);
        Out.SetValue(LogicState.Zero);
    }

    public override void Evaluate()
    {
        if (BitWidth == 1)
        {
            Set(Out, Read(In[0]));
        }
        else
        {
            // Если внешняя шина НЕ подключена к 0-му пину, берем данные с переключателей
            bool isDrivenByBus = In[0].BusStates.Any(s => s != LogicState.HighZ);

            if (isDrivenByBus)
            {
                for (int i = 0; i < BitWidth; i++) Out.BusStates[i] = ReadBusBit(In[0], i);
            }
            else
            {
                for (int i = 0; i < BitWidth; i++) Out.BusStates[i] = Read(In[i]);
            }

            bool anyConflict = false, anyOne = false, anyDriven = false;
            for (int i = 0; i < BitWidth; i++)
            {
                var s = Out.BusStates[i];
                if (s == LogicState.Conflict) anyConflict = true;
                else if (s == LogicState.One) anyOne = true;
                if (s != LogicState.HighZ) anyDriven = true;
            }
            for (int i = BitWidth; i < 32; i++) Out.BusStates[i] = LogicState.HighZ;

            if (anyConflict) Set(Out, LogicState.Conflict);
            else if (anyOne) Set(Out, LogicState.One);
            else if (anyDriven) Set(Out, LogicState.Zero);
            else Set(Out, LogicState.HighZ);
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
        In.ResetBusStates();
        ResizeLabels(InputLabels, 1, _ => "1");
        ResizeLabels(OutputLabels, BitWidth, i => (i + 1).ToString());
    }

    public override void Evaluate()
    {
        if (BitWidth == 1)
        {
            Set(InternalOut[0], Read(In));
        }
        else
        {
            for (int i = 0; i < BitWidth; i++)
                InternalOut[i].SetValue(ReadBusBit(In, i));
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
        var a = Read(A);
        var b = Read(B);

        if (a == LogicState.Zero || b == LogicState.Zero) { Set(Out, LogicState.One); return; }
        if (a == LogicState.Conflict || b == LogicState.Conflict) { Set(Out, LogicState.Conflict); return; }
        if (a == LogicState.HighZ || b == LogicState.HighZ) { Set(Out, LogicState.HighZ); return; }
        Set(Out, LogicState.Zero);
    }
}

/// <summary>Трёхсостоятельный буфер: E=1 → данные, E=0 → Z.</summary>
public sealed class TriStateBufferElement : Element
{
    public override string Name => "3STATE";

    public InputPin Data { get; } = new();
    public InputPin Enable { get; } = new();
    public OutputPin Out { get; } = new();

    public override IReadOnlyList<InputPin> Inputs => new[] { Data, Enable };
    public override IReadOnlyList<OutputPin> Outputs => new[] { Out };

    protected override IEnumerable<string> DefaultInputLabels() => new[] { "D", "E" };
    protected override IEnumerable<string> DefaultOutputLabels() => new[] { "Y" };

    public override void Evaluate()
    {
        var e = Read(Enable);

        // Неподключённый/висящий enable трактуем как выключенный: выход Z, а не X.
        if (e == LogicState.Zero || e == LogicState.HighZ)
        {
            Set(Out, LogicState.HighZ);
            return;
        }
        if (e == LogicState.Conflict)
        {
            Set(Out, LogicState.Conflict);
            return;
        }

        Set(Out, Read(Data));
    }
}

/// <summary>Слабый источник логической единицы (аналог подтяжки к VCC).</summary>
public sealed class PullUpElement : Element
{
    public override string Name => "PULL UP";

    public OutputPin Out { get; } = new();

    public override IReadOnlyList<InputPin> Inputs => Array.Empty<InputPin>();
    public override IReadOnlyList<OutputPin> Outputs => new[] { Out };

    protected override IEnumerable<string> DefaultOutputLabels() => new[] { "Y" };

    public PullUpElement() => Out.Weak = true;

    public override void Evaluate() => Set(Out, LogicState.One);
}

/// <summary>Слабый источник логического нуля (аналог подтяжки к GND).</summary>
public sealed class PullDownElement : Element
{
    public override string Name => "PULL DOWN";

    public OutputPin Out { get; } = new();

    public override IReadOnlyList<InputPin> Inputs => Array.Empty<InputPin>();
    public override IReadOnlyList<OutputPin> Outputs => new[] { Out };

    protected override IEnumerable<string> DefaultOutputLabels() => new[] { "Y" };

    public PullDownElement() => Out.Weak = true;

    public override void Evaluate() => Set(Out, LogicState.Zero);
}

/// <summary>Элемент с внутренним состоянием, который обновляется один раз за такт симуляции.</summary>
public interface ITickable
{
    void Advance();
}

public sealed class ClockElement : Element, ITickable
{
    public override string Name => "CLK";

    /// <summary>Полный период в тактах (половина — высокий уровень).</summary>
    public int PeriodTicks { get; private set; } = 60;

    private int _phase;

    public OutputPin Out { get; } = new();
    public override IReadOnlyList<InputPin> Inputs => Array.Empty<InputPin>();
    public override IReadOnlyList<OutputPin> Outputs => new[] { Out };

    protected override IEnumerable<string> DefaultOutputLabels() => new[] { "Q" };

    public ClockElement() => SetPeriod(PeriodTicks);

    public void SetPeriod(int ticks)
    {
        PeriodTicks = Math.Clamp(ticks, 2, 600);
        _phase = 0;
        Set(Out, LogicState.Zero);
    }

    public void Advance()
    {
        int half = Math.Max(1, PeriodTicks / 2);
        if (++_phase < half) return;
        _phase = 0;
        Set(Out, Out.State == LogicState.One ? LogicState.Zero : LogicState.One);
    }

    // Выходом управляет Advance(), поэтому повторные проходы Evaluate() его не трогают.
    public override void Evaluate() { }
}

public sealed class DelayElement : Element, ITickable
{
    public override string Name => "DELAY";

    public int DelayTicks { get; private set; } = 8;

    private LogicState[][] _buffer = Array.Empty<LogicState[]>();
    private int _head;

    public InputPin In { get; } = new();
    public OutputPin Out { get; } = new();

    public override IReadOnlyList<InputPin> Inputs => new[] { In };
    public override IReadOnlyList<OutputPin> Outputs => new[] { Out };

    protected override IEnumerable<string> DefaultInputLabels() => new[] { "D" };
    protected override IEnumerable<string> DefaultOutputLabels() => new[] { "Q" };

    public DelayElement(int delayTicks = 8) => SetDelay(delayTicks);

    public void SetDelay(int ticks)
    {
        DelayTicks = Math.Clamp(ticks, 1, 120);
        _buffer = Enumerable.Range(0, DelayTicks + 1)
            .Select(_ => new LogicState[32])
            .ToArray();
        _head = 0;
        Out.ResetBusStates();
        Set(Out, LogicState.HighZ);
    }

    public void Advance()
    {
        // Пиши в текущую ячейку, читаем самую старую — получаем задержку ровно в DelayTicks тактов.
        var slot = _buffer[_head];
        slot[0] = Read(In);
        for (int i = 1; i < 32; i++) slot[i] = LogicState.HighZ;

        int oldest = (_head + 1) % _buffer.Length;
        var src = _buffer[oldest];

        for (int i = 0; i < 32; i++) Out.BusStates[i] = src[i];
        Set(Out, Circuit.Aggregate(src));
        _head = oldest;
    }

    public override void Evaluate() { }
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

        ResizeLabels(InputLabels, BitWidth, i => (i + 1).ToString());
        ResizeLabels(OutputLabels, 1, _ => "Bus");
        Out.BitWidth = BitWidth;
        Out.ResetBusStates();
    }

    public override void Evaluate()
    {
        bool anyConflict = false;
        bool anyOne = false;
        bool anyDriven = false;

        for (int i = 0; i < BitWidth; i++)
        {
            // Если на вход уже пришла шина, берём её младший бит, а не агрегат.
            var state = In[i].BitWidth > 1 ? ReadBusBit(In[i], 0) : Read(In[i]);

            Out.BusStates[i] = state;

            if (state == LogicState.Conflict) anyConflict = true;
            else if (state == LogicState.One) anyOne = true;
            if (state != LogicState.HighZ) anyDriven = true;
        }

        // Старые биты выше текущей разрядности не должны просачиваться в шину.
        for (int i = BitWidth; i < 32; i++) Out.BusStates[i] = LogicState.HighZ;

        if (anyConflict) Set(Out, LogicState.Conflict);
        else if (anyOne) Set(Out, LogicState.One);
        else if (anyDriven) Set(Out, LogicState.Zero);
        else Set(Out, LogicState.HighZ);
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

        ResizeLabels(InputLabels, 1, _ => "Bus");
        ResizeLabels(OutputLabels, BitWidth, i => (i + 1).ToString());
        In.BitWidth = BitWidth;
        In.ResetBusStates();
    }

    public override void Evaluate()
    {
        // Агрегат In.State может быть рассинхронизирован с битами — ориентируемся только на биты.
        for (int i = 0; i < BitWidth; i++)
            Out[i].SetValue(ReadBusBit(In, i));
    }
}

public sealed class ChipElement : Element, ITickable
{
    public const int MinWidth = 80;
    public const int MinHeight = 50;
    // Диаметр визуального пина 20 px + зона попадания 14 px: 18 px плотно
    // склеивали сотни пинов в сплошную полосу.
    public const int MinPinSpacing = 28;

    private readonly List<Element> _inner;
    private readonly List<Wire> _innerWires;
    private readonly (InputPin ext, InputPin inner)[] _inputs;
    private readonly (InputPin inner, OutputPin ext)[] _outputs;
    private readonly Dictionary<Element, (int X, int Y)> _innerPositions = new();
    private readonly Dictionary<Pin, float> _pinOffsets = new();

    // Кэш выходных пинов внутренней схемы: нужен, чтобы понять, что комбинационная
    // логика уже успокоилась и ещё один проход ничего не изменит.
    private OutputPin[] _trackedOutputs = Array.Empty<OutputPin>();
    private LogicState[] _outputSnapshot = Array.Empty<LogicState>();
    private bool _snapshotValid;

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
        // Высота по умолчанию 80 px рассчитана на единичные пины. Чип со 1000
        // входами не должен существовать с такой высотой даже между вызовами SetSize().
        SetSize(_width, _height);
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

    /// <summary>
    /// Отступ от края в долях высоты. Должен быть меньше шага между пинами,
    /// иначе при большом количестве входов все крайние пины схлопываются
    /// в одну точку (1000 входов → ~80 пинов на 0.08).
    /// </summary>
    public static float PinMargin(int count)
        => Math.Min(0.08f, 0.5f / Math.Max(1, count + 1));

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

    public static int PinVisualHeight(Pin pin)
        => pin.BitWidth > 1 ? Math.Max(4, pin.BitWidth * 2) : 0;

    /// <summary>Минимальная высота корпуса с учётом высоты шинных пинов.</summary>
    public static int MinHeightFor(IReadOnlyList<Pin> inputs, IReadOnlyList<Pin> outputs)
    {
        int count = Math.Max(inputs.Count, outputs.Count);
        int maxPinH = 0;
        foreach (var pin in inputs) maxPinH = Math.Max(maxPinH, PinVisualHeight(pin));
        foreach (var pin in outputs) maxPinH = Math.Max(maxPinH, PinVisualHeight(pin));

        int byCount = Math.Max(MinHeight, count * MinPinSpacing + 20);
        int byBus = (count + 1) * (maxPinH + 6);
        return Math.Max(byCount, byBus);
    }

    /// <summary>
    /// Восстановить соответствие внешних пинов внутренним IN/OUT после изменения
    /// разрядности шины, добавления/удаления элементов или загрузки снимка.
    /// Внешние пины не пересоздаются — провода и подписи остаются на месте.
    /// </summary>
    public void SyncExternalPins()
    {
        var ins = _inner.OfType<InElement>().ToList();
        for (int i = 0; i < _inputs.Length; i++)
        {
            var (ext, oldInner) = _inputs[i];
            var owner = ins.FirstOrDefault(ie => ie.In.Any(p => ReferenceEquals(p, oldInner)));
            int idx = owner is null ? i : ins.IndexOf(owner);
            if (idx >= ins.Count) continue;

            var ie = ins[idx];
            if (ie.In.Length == 0) continue;
            SyncWidth(ext, ie.Out.BitWidth);
            _inputs[i] = (ext, ie.In[0]);
        }

        var outs = _inner.OfType<OutElement>().ToList();
        for (int i = 0; i < _outputs.Length; i++)
        {
            var (oldInner, ext) = _outputs[i];
            var owner = outs.FirstOrDefault(oe => ReferenceEquals(oe.In, oldInner));
            int idx = owner is null ? i : outs.IndexOf(owner);
            if (idx >= outs.Count) continue;

            var oe = outs[idx];
            SyncWidth(ext, oe.In.BitWidth);
            _outputs[i] = (oe.In, ext);
        }
    }

    private static void SyncWidth(Pin ext, int width)
    {
        if (ext.BitWidth == width) return;
        ext.BitWidth = width;
        ext.ResetBusStates();
        ext.SetValue(LogicState.HighZ);
    }

    public void RemapExternalPins() => SyncExternalPins();

    public void SetSize(int w, int h)
    {
        int minH = MinHeightFor(Inputs, Outputs);
        _width = Math.Max(MinWidth, w);
        _height = Math.Max(minH, h);
    }

    public float GetOffset(Pin pin)
        => _pinOffsets.TryGetValue(pin, out var v) ? v : 0.5f;

    public void SetOffset(Pin pin, float value)
    {
        int count = PinCount(pin);
        float margin = PinMargin(count);
        if (pin.BitWidth > 1)
        {
            // Шинный пин прямоугольный: не даём ему вылезти за корпус.
            float busMargin = (PinVisualHeight(pin) / 2f + 4f) / Math.Max(1, Height);
            margin = Math.Max(margin, Math.Min(0.45f, busMargin));
        }
        _pinOffsets[pin] = Math.Clamp(value, margin, 1f - margin);
    }

    private int PinCount(Pin pin)
    {
        foreach (var p in Inputs) if (ReferenceEquals(p, pin)) return Inputs.Count;
        foreach (var p in Outputs) if (ReferenceEquals(p, pin)) return Outputs.Count;
        return 1;
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

    public void Advance()
    {
        foreach (var el in _inner)
            if (el is ITickable tickable)
                tickable.Advance();
    }

    public override void Evaluate()
    {
        foreach (var (ext, inner) in _inputs)
        {
            // Нет внешнего провода — внутренний IN остаётся переключателем.
            // Инъекция Z в этом случае стирала бы пользовательские биты.
            if (!ext.IsWired && ext.BusStates.All(s => s == LogicState.HighZ)) continue;

            int bits = Math.Min(ext.BitWidth, 32);
            for (int i = 0; i < 32; i++)
                inner.BusStates[i] = i < bits ? ReadBusBit(ext, i) : LogicState.HighZ;
            inner.SetValue(Read(ext));
        }

        _snapshotValid = false;
        int passes = _inner.Count + 2;
        for (int p = 0; p < passes; p++)
        {
            Circuit.ResolveInputs(_inner, _innerWires);
            foreach (var el in _inner) el.Evaluate();

            // Раньше делалось ровно N+2 проходов всегда. Для вложенных чипов это
            // превращалось в O(N^2) по каждому тику (и хуже по глубине вложенности):
            // один 8-битный сумматор из макросов не влезал в кадр.
            // Как только ни один выход не изменился — неподвижная точка достигнута.
            if (OutputsUnchanged()) break;
        }

        foreach (var (inner, ext) in _outputs)
        {
            int bits = Math.Min(ext.BitWidth, 32);
            for (int i = 0; i < 32; i++)
                ext.BusStates[i] = i < bits ? ReadBusBit(inner, i) : LogicState.HighZ;
            ext.SetValue(Read(inner));
        }
    }

    /// <summary>
    /// Сравнивает выходы внутренних элементов с предыдущим проходом.
    /// Первый вызов только запоминает состояние и возвращает false.
    /// </summary>
    private bool OutputsUnchanged()
    {
        var current = _inner.SelectMany(el => el.Outputs).ToArray();

        if (_trackedOutputs.Length != current.Length)
        {
            _trackedOutputs = current;
            _outputSnapshot = new LogicState[current.Length * 33];
            _snapshotValid = false;
        }
        else
        {
            for (int i = 0; i < current.Length; i++)
            {
                if (ReferenceEquals(_trackedOutputs[i], current[i])) continue;
                _trackedOutputs = current;
                _outputSnapshot = new LogicState[current.Length * 33];
                _snapshotValid = false;
                break;
            }
        }

        bool unchanged = _snapshotValid;
        int idx = 0;
        foreach (var pin in _trackedOutputs)
        {
            if (_snapshotValid && _outputSnapshot[idx] != pin.State) unchanged = false;
            _outputSnapshot[idx++] = pin.State;

            for (int i = 0; i < 32; i++)
            {
                if (_snapshotValid && _outputSnapshot[idx] != pin.BusStates[i]) unchanged = false;
                _outputSnapshot[idx++] = pin.BusStates[i];
            }
        }
        _snapshotValid = true;
        return unchanged;
    }

    public ChipElement Clone()
    {
        // Внутренние пины могли быть пересозданы сменой разрядности — починим
        // соответствие до клонирования, иначе FindOwner молча выбросит пины.
        SyncExternalPins();

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
            nw.IsBus = nw.IsValid && w.IsBus;
            nw.Layer = w.Layer;
            newWires.Add(nw);
        }

        var newInputs = new List<(InputPin ext, InputPin inner)>();
        foreach (var (ext, inner) in _inputs)
        {
            var owner = FindOwner(inner);
            if (owner is null) continue;
            // Разрядность внешнего пина берём из ext: внутренний In[0] у шинного IN
            // всегда однобитный, и клонирование превращало шину в обычный провод.
            var newExt = new InputPin { BitWidth = ext.BitWidth };
            newInputs.Add((newExt, (InputPin)MapPin(owner, inner, map[owner])));
        }

        var newOutputs = new List<(InputPin inner, OutputPin ext)>();
        foreach (var (inner, ext) in _outputs)
        {
            var owner = FindOwner(inner);
            if (owner is null) continue;
            var newExt = new OutputPin { BitWidth = ext.BitWidth };
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
            TriStateBufferElement => new TriStateBufferElement(),
            PullUpElement => new PullUpElement(),
            PullDownElement => new PullDownElement(),
            ClockElement => new ClockElement(),
            DelayElement de => new DelayElement(de.DelayTicks),
            InElement ie => new InElement(ie.BitWidth),
            OutElement oe => new OutElement(oe.BitWidth),
            BusMerger bm => new BusMerger(bm.BitWidth),
            BusSplitter bs => new BusSplitter(bs.BitWidth),
            ChipElement ce => ce.Clone(),
            _ => new NandElement()
        };

        copy.Layer = el.Layer;
        copy.BodyColor = el.BodyColor;
        copy.WireColor = el.WireColor;

        if (el is ClockElement oldClock && copy is ClockElement newClock)
            newClock.SetPeriod(oldClock.PeriodTicks);
        if (el is DelayElement oldDelay && copy is DelayElement newDelay)
            newDelay.SetDelay(oldDelay.DelayTicks);

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