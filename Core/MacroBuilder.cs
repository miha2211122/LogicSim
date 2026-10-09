using System.Numerics;
using SDColor = System.Drawing.Color;

namespace LogicSim.Game.Core;

public sealed class MacroRecipe
{
    public List<Element> SourceElements = new();
    public List<Element> Inner = new();
    public List<Wire> InnerWires = new();
    public List<(InputPin ext, InputPin inner)> Inputs = new();
    public List<(OutputPin inner, OutputPin ext)> Outputs = new();
    public List<string> InputLabels = new();
    public List<string> OutputLabels = new();
    public List<float> InputOffsets = new();
    public List<float> OutputOffsets = new();
    public List<SDColor?> InputPinColors = new();
    public List<SDColor?> OutputPinColors = new();
    public int Width = Element.DefaultWidth;
    public int Height = Element.DefaultHeight;
    public List<Wire> ExternalRewire = new();
    public Vector2 Center;
}

public static class MacroBuilder
{
    public static MacroRecipe? Prepare(Circuit circuit, IReadOnlyList<Element> selection)
    {
        if (selection.Count == 0) return null;

        var recipe = new MacroRecipe();
        recipe.SourceElements.AddRange(selection);

        var selectedSet = new HashSet<Element>(selection);
        var extIn = new Dictionary<InElement, InputPin>();
        var extOut = new Dictionary<OutElement, OutputPin>();

        int inCounter = 0, outCounter = 0;

        foreach (var el in selection)
        {
            recipe.Inner.Add(el);
            switch (el)
            {
                case InElement ie:
                    var inExt = new InputPin();
                    recipe.Inputs.Add((inExt, ie.In));
                    recipe.InputLabels.Add($"IN{++inCounter}");
                    extIn[ie] = inExt;
                    break;
                case OutElement oe:
                    var outExt = new OutputPin();
                    recipe.Outputs.Add((oe.Out, outExt));
                    recipe.OutputLabels.Add($"OUT{++outCounter}");
                    extOut[oe] = outExt;
                    break;
            }
        }

        for (int i = 0; i < recipe.Inputs.Count; i++)
        {
            recipe.InputOffsets.Add(ChipElement.DefaultOffset(recipe.Inputs.Count, i));
            recipe.InputPinColors.Add(null);
        }
        for (int i = 0; i < recipe.Outputs.Count; i++)
        {
            recipe.OutputOffsets.Add(ChipElement.DefaultOffset(recipe.Outputs.Count, i));
            recipe.OutputPinColors.Add(null);
        }

        foreach (var w in circuit.Wires)
        {
            var fromOwner = circuit.OwnerOf(w.From);
            var toOwner = circuit.OwnerOf(w.To);
            if (fromOwner is null || toOwner is null) continue;

            bool fromIn = selectedSet.Contains(fromOwner);
            bool toIn = selectedSet.Contains(toOwner);

            if (fromIn && toIn)
            {
                recipe.InnerWires.Add(w);
            }
            else if (!fromIn && toIn && toOwner is InElement ie)
            {
                var nw = new Wire(w.From, extIn[ie]);
                foreach (var wp in w.Waypoints) nw.Waypoints.Add(wp);
                recipe.ExternalRewire.Add(nw);
            }
            else if (fromIn && !toIn && fromOwner is OutElement oe)
            {
                var nw = new Wire(extOut[oe], w.To);
                foreach (var wp in w.Waypoints) nw.Waypoints.Add(wp);
                recipe.ExternalRewire.Add(nw);
            }
        }

        return recipe;
    }

    public static ChipElement Commit(
        Circuit circuit, MacroRecipe recipe, string name,
        SDColor bodyColor, SDColor wireColor)
    {
        var chip = new ChipElement(
            name,
            recipe.Inner.ToArray(),
            recipe.InnerWires.ToArray(),
            recipe.Inputs.ToArray(),
            recipe.Outputs.ToArray());

        chip.BodyColor = bodyColor;
        chip.WireColor = wireColor;
        chip.SetSize(recipe.Width, recipe.Height);

        for (int i = 0; i < recipe.Inputs.Count && i < chip.Inputs.Count; i++)
        {
            chip.SetOffset(chip.Inputs[i], recipe.InputOffsets[i]);
            if (i < recipe.InputPinColors.Count && recipe.InputPinColors[i] is SDColor ic)
                chip.Inputs[i].Color = ic;
        }
        for (int i = 0; i < recipe.Outputs.Count && i < chip.Outputs.Count; i++)
        {
            chip.SetOffset(chip.Outputs[i], recipe.OutputOffsets[i]);
            if (i < recipe.OutputPinColors.Count && recipe.OutputPinColors[i] is SDColor oc)
                chip.Outputs[i].Color = oc;
        }

        circuit.RemoveRange(recipe.SourceElements);
        circuit.Add(chip);

        foreach (var w in recipe.ExternalRewire)
            circuit.WiresList.Add(w);

        return chip;
    }
}