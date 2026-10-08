using LogicSim.Game.Core;
using SDColor = System.Drawing.Color;

namespace LogicSim.Game.Rendering;

public sealed class ChipTemplate
{
    public required string Name { get; init; }
    public required SDColor BodyColor { get; init; }
    public required SDColor WireColor { get; init; }
    public required ChipElement Prototype { get; init; }
}