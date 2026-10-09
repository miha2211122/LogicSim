using LogicSim.Game.Core;
using System.Numerics;

namespace LogicSim.Game.Rendering;

/// <summary>
/// Состояние протяжки провода: от какого выхода тянем, куда уже поставили
/// waypoints, и завершение на приёмном входе.
/// </summary>
public sealed class WireDrawer
{
    public Element? FromElement { get; private set; }
    public OutputPin? FromPin { get; private set; }
    public List<Vector2> Waypoints { get; } = new();
    public bool IsActive => FromPin is not null;

    public void Begin(Element owner, OutputPin pin)
    {
        FromElement = owner;
        FromPin = pin;
        Waypoints.Clear();
    }

    public void AddWaypoint(Vector2 world) => Waypoints.Add(world);

    public void Cancel()
    {
        FromElement = null;
        FromPin = null;
        Waypoints.Clear();
    }

    /// <summary>Завершает провод на входном пине. Возвращает созданный провод или null.</summary>
    public Wire? TryFinish(Circuit circuit, PinHit hit)
    {
        if (FromPin is null) return null;
        if (!hit.IsInput) return null;
        if (hit.Pin is not InputPin input) return null;

        var wire = circuit.Connect(FromPin, input);
        foreach (var wp in Waypoints) wire.Waypoints.Add(wp);
        Cancel();
        return wire;
    }
}