using System.Numerics;

namespace LogicSim.Game.Core;

public sealed class Wire
{
    public OutputPin From { get; }
    public InputPin To { get; }
    public List<Vector2> Waypoints { get; } = new();

    /// <summary>Номер слоя, к которому принадлежит провод.</summary>
    public int Layer { get; set; }

    /// <summary>Является ли этот провод частью многобитной шины.</summary>
    public bool IsBus { get; set; } = false;

    public Wire(OutputPin from, InputPin to)
    {
        From = from;
        To = to;
        IsBus = from.BitWidth > 1 || to.BitWidth > 1;
    }

    /// <summary>Провод корректен, только если разрядность обоих пинов совпадает.</summary>
    public bool IsValid => From.BitWidth == To.BitWidth;
}