namespace LogicSim.Game.Core;

public abstract class Pin
{
    public bool Value { get; protected set; }
    public abstract void SetValue(bool value);
}

public sealed class InputPin : Pin
{
    public override void SetValue(bool value) => Value = value;
    public void Toggle() => Value = !Value;
}

public sealed class OutputPin : Pin
{
    public override void SetValue(bool value) => Value = value;
}