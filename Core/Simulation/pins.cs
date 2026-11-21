namespace LogicSim.Game.Core;

public enum LogicState { Zero, One, HighZ, Conflict }

public abstract class Pin
{
    public LogicState State { get; protected set; } = LogicState.HighZ;
    public bool Value => State == LogicState.One;

    /// <summary>Приходит ли в пин хоть один провод (обновляется NetSolver на каждом проходе).</summary>
    public bool IsWired { get; internal set; }

    /// <summary>Массив состояний для шин (до 32 бит). По умолчанию все HighZ.</summary>
    private LogicState[] _busStates = Enumerable.Repeat(LogicState.HighZ, 32).ToArray();
    public LogicState[] BusStates
    {
        get => _busStates;
        set
        {
            // Всегда держим ровно 32 ячейки и не даём внешнему коду алиасить массив.
            Array.Fill(_busStates, LogicState.HighZ);
            if (value is not null)
                Array.Copy(value, _busStates, Math.Min(value.Length, _busStates.Length));
        }
    }

    /// <summary>Сбрасывает все биты в High-Z (используется при отключении шины).</summary>
    public void ResetBusStates() => Array.Fill(_busStates, LogicState.HighZ);

    /// <summary>Разрядность пина (1 - обычный провод, >1 - шина).</summary>
    public int BitWidth { get; set; } = 1;

    /// <summary>Слабый драйвер (подтяжка): сильный драйвер перекрывает его без конфликта.</summary>
    public bool Weak { get; set; }

    public System.Drawing.Color? Color { get; set; }

    public void SetValue(bool value) => State = value ? LogicState.One : LogicState.Zero;
    public void SetValue(LogicState state) => State = state;
}

public sealed class InputPin : Pin
{
    public void Toggle() => State = State == LogicState.One ? LogicState.Zero : LogicState.One;
}

public sealed class OutputPin : Pin { }