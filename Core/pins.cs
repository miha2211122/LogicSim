namespace LogicSim.Game.Core;

public enum LogicState { Zero, One, HighZ, Conflict }

public abstract class Pin
{
    public LogicState State { get; protected set; } = LogicState.HighZ;
    public bool Value => State == LogicState.One;

    /// <summary>Индивидуальный цвет пина. null = использовать WireColor владельца.</summary>
    public System.Drawing.Color? Color { get; set; }

    public void SetValue(bool value) => State = value ? LogicState.One : LogicState.Zero;
    public void SetValue(LogicState state) => State = state;
}

public sealed class InputPin : Pin
{
    public void Toggle() => State = State == LogicState.One ? LogicState.Zero : LogicState.One;
}

public sealed class OutputPin : Pin { }