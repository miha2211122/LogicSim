namespace LogicSim.Game.Core;

public enum LogicState { Zero, One, HighZ, Conflict }

public abstract class Pin
{
    public LogicState State { get; protected set; } = LogicState.HighZ;
    public bool Value => State == LogicState.One;

    /// <summary>Массив состояний для шин (до 32 бит). По умолчанию все HighZ.</summary>
    private LogicState[] _busStates = Enumerable.Repeat(LogicState.HighZ, 32).ToArray();
    public LogicState[] BusStates { get => _busStates; set => _busStates = value; }

    /// <summary>Разрядность пина (1 - обычный провод, >1 - шина).</summary>
    public int BitWidth { get; set; } = 1;

    public System.Drawing.Color? Color { get; set; }

    public void SetValue(bool value) => State = value ? LogicState.One : LogicState.Zero;
    public void SetValue(LogicState state) => State = state;
}

public sealed class InputPin : Pin
{
    public void Toggle() => State = State == LogicState.One ? LogicState.Zero : LogicState.One;
}

public sealed class OutputPin : Pin { }