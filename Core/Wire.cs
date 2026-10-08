using System.Numerics;

namespace LogicSim.Game.Core;

/// <summary>
/// Провод между выходом и входом. Может иметь промежуточные точки (waypoints).
/// </summary>
public sealed class Wire
{
    public OutputPin From { get; }
    public InputPin To { get; }
    public List<Vector2> Waypoints { get; } = new();

    public Wire(OutputPin from, InputPin to)
    {
        From = from;
        To = to;
    }
}