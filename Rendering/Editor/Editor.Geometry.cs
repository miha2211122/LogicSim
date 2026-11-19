using LogicSim.Game.Core;
using Raylib_cs;
using System.Numerics;

namespace LogicSim.Game.Rendering;

public sealed partial class Editor
{
    private bool IsNearAnyPin(Vector2 p)
    {
        foreach (var el in Ctx.Circuit.Elements)
        {
            if (!IsElementVisible(el)) continue;
            if (!Ctx.Layout.TryGet(el, out var pos)) continue;

            for (int i = 0; i < el.Inputs.Count; i++)
            {
                int py = Ctx.Layout.PinScreenY(el, el.Inputs[i], isInput: true);
                if (Vector2.Distance(p, new Vector2(pos.X, py)) <= Layout.HitPinRadius) return true;
            }
            for (int i = 0; i < el.Outputs.Count; i++)
            {
                int py = Ctx.Layout.PinScreenY(el, el.Outputs[i], isInput: false);
                if (Vector2.Distance(p, new Vector2(pos.X + el.Width, py)) <= Layout.HitPinRadius) return true;
            }
        }
        return false;
    }

    private string GetPinLabel(PinHit hit)
    {
        if (hit.IsInput)
        {
            int idx = Layout.PinIndex(hit.Element, hit.Pin, isInput: true);
            if (idx >= 0 && idx < hit.Element.InputLabels.Count) return hit.Element.InputLabels[idx];
        }
        else
        {
            int idx = Layout.PinIndex(hit.Element, hit.Pin, isInput: false);
            if (idx >= 0 && idx < hit.Element.OutputLabels.Count) return hit.Element.OutputLabels[idx];
        }
        return "";
    }

    private void ApplyPinRename(PinHit hit, string newName)
    {
        if (string.IsNullOrEmpty(newName)) return;

        BeginChange();

        if (hit.IsInput)
        {
            int idx = Layout.PinIndex(hit.Element, hit.Pin, isInput: true);
            if (idx >= 0 && idx < hit.Element.InputLabels.Count)
                hit.Element.InputLabels[idx] = newName;
        }
        else
        {
            int idx = Layout.PinIndex(hit.Element, hit.Pin, isInput: false);
            if (idx >= 0 && idx < hit.Element.OutputLabels.Count)
                hit.Element.OutputLabels[idx] = newName;
        }

        Ctx.OwnerChip?.ResyncExternalLabels();
    }

    private Wire? FindWireAt(Vector2 p)
    {
        const float threshold = 12f;

        foreach (var wire in Ctx.Circuit.Wires)
        {
            if (!IsWireVisible(wire)) continue;
            var owner1 = Ctx.Circuit.OwnerOf(wire.From);
            var owner2 = Ctx.Circuit.OwnerOf(wire.To);
            if (owner1 is null || owner2 is null) continue;

            var p1 = Ctx.Layout.PinPosition(owner1, wire.From);
            var p2 = Ctx.Layout.PinPosition(owner2, wire.To);
            if (p1 is null || p2 is null) continue;

            var pts = new List<Vector2> { p1.Value };
            pts.AddRange(wire.Waypoints);
            pts.Add(p2.Value);

            for (int i = 0; i + 1 < pts.Count; i++)
                if (DistanceToSegment(p, pts[i], pts[i + 1]) <= threshold)
                    return wire;
        }
        return null;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float len2 = ab.LengthSquared();
        if (len2 < 0.0001f) return Vector2.Distance(p, a);
        float t = Math.Clamp(Vector2.Dot(p - a, ab) / len2, 0f, 1f);
        return Vector2.Distance(p, a + ab * t);
    }
}
