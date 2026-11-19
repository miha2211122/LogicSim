using LogicSim.Game.Core;
using System.Numerics;
using SDColor = System.Drawing.Color;

namespace LogicSim.Game.Rendering;

public sealed class ChipTemplate
{
    public required string Name { get; init; }
    public required SDColor BodyColor { get; init; }
    public required SDColor WireColor { get; init; }
    public required ChipElement Prototype { get; init; }
    public Vector2 CameraTarget { get; init; }
    public float CameraZoom { get; init; }
}