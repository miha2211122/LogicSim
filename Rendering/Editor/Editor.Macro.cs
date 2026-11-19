using LogicSim.Game.Core;
using Raylib_cs;
using System.Numerics;

namespace LogicSim.Game.Rendering;

public sealed partial class Editor
{
    private static LayerSet CopyLayers(LayerSet source)
    {
        var copy = new LayerSet();
        copy.CopyFrom(source);
        return copy;
    }

    private void EnterChip(ChipElement chip, bool readonlyMode, string? templateName = null)
    {
        var layout = new Layout();
        foreach (var el in chip.Inner)
        {
            if (chip.InnerPositions.TryGetValue(el, out var pos)) layout.Place(el, pos.X, pos.Y);
            else layout.Place(el, 100, 100);
        }

        var circuit = new Circuit();
        foreach (var el in chip.Inner) circuit.AddElement(el);
        foreach (var w in chip.InnerWires) circuit.WiresList.Add(w);

        _stack.Push(new Context
        {
            Circuit = circuit,
            Layout = layout,
            Layers = readonlyMode ? CopyLayers(chip.LayerState) : chip.LayerState,
            Name = chip.Name,
            ReadOnly = readonlyMode,
            OwnerChip = chip,
            TemplateName = templateName,
            History = !readonlyMode && _chipHistories.TryGetValue(chip, out var savedHistory)
                ? savedHistory
                : new History()
        });

        _selection.Clear();
        _wireDrawer.Cancel();
        _clipboard.CancelHologram();
    }

    private void EnterChipAsTemplate(ChipTemplate tpl, ref Camera2D camera)
    {
        EnterChip(tpl.Prototype, readonlyMode: false, templateName: tpl.Name);

        if (tpl.CameraZoom > 0)
        {
            camera.Target = tpl.CameraTarget;
            camera.Zoom = tpl.CameraZoom;
        }

        StatusMessage = $"Editing macro '{tpl.Name}'. Changes apply to all instances.";
        _statusTimer = 3f;
    }

    private void ExitChip()
    {
        if (_stack.Count <= 1) return;
        var top = _stack.Pop();

        if (!top.ReadOnly && top.OwnerChip is { } historyOwner)
            _chipHistories[historyOwner] = top.History;

        if (top.OwnerChip is not null)
        {
            if (!top.ReadOnly)
            {
                top.OwnerChip.LayerState.CopyFrom(top.Layers);
                top.OwnerChip.SyncInner(top.Circuit.Elements, top.Circuit.WiresList);
                top.OwnerChip.ResyncExternalLabels();
            }

            foreach (var el in top.OwnerChip.Inner)
                if (top.Layout.TryGet(el, out var p))
                    top.OwnerChip.SetInnerPosition(el, p.X, p.Y);
        }

        if (top.TemplateName is { } tname)
            RebuildInstancesFromTemplate(tname);

        _selection.Clear();
        _wireDrawer.Cancel();
        _clipboard.CancelHologram();
    }

    private static Element CloneForBatch(Palette.Tool tool)
        => Palette.CreateElement(tool) ?? throw new InvalidOperationException();

    private bool IsDriven(InputPin pin) => Ctx.Circuit.Wires.Any(w => ReferenceEquals(w.To, pin));

    private ChipTemplate? FindTemplateByName(string name)
    {
        foreach (var t in _palette.Templates) if (t.Name == name) return t;
        foreach (var t in _palette.Library) if (t.Name == name) return t;
        return null;
    }

    private void RebuildInstancesFromTemplate(string templateName)
    {
        var tpl = FindTemplateByName(templateName);
        if (tpl is null) return;

        var root = _stack.Last();
        var proto = tpl.Prototype;

        var instances = root.Circuit.Elements
            .OfType<ChipElement>()
            .Where(c => c.Name == templateName && !ReferenceEquals(c, proto))
            .ToList();

        foreach (var inst in instances)
        {
            ReplaceInstance(root.Circuit, root.Layout, inst, proto);
        }
    }

    private void ReplaceInstance(Circuit circuit, Layout layout, ChipElement oldChip, ChipElement proto)
    {
        if (!layout.TryGet(oldChip, out var pos)) return;

        var incoming = new List<(int idx, OutputPin from, List<Vector2> wps, int layer)>();
        var outgoing = new List<(int idx, InputPin to, List<Vector2> wps, int layer)>();

        foreach (var w in circuit.Wires)
        {
            var ownerFrom = circuit.OwnerOf(w.From);
            var ownerTo = circuit.OwnerOf(w.To);

            if (ReferenceEquals(ownerTo, oldChip))
            {
                int idx = Layout.PinIndex(oldChip, w.To, isInput: true);
                if (idx >= 0) incoming.Add((idx, w.From, w.Waypoints.ToList(), w.Layer));
            }
            if (ReferenceEquals(ownerFrom, oldChip))
            {
                int idx = Layout.PinIndex(oldChip, w.From, isInput: false);
                if (idx >= 0) outgoing.Add((idx, w.To, w.Waypoints.ToList(), w.Layer));
            }
        }

        circuit.Remove(oldChip);
        layout.Remove(oldChip);

        var fresh = proto.Clone();
        fresh.Layer = oldChip.Layer;
        circuit.Add(fresh);
        layout.Place(fresh, pos.X, pos.Y);

        foreach (var (idx, from, wps, layer) in incoming)
        {
            if (idx >= fresh.Inputs.Count) continue;
            var nw = circuit.Connect(from, fresh.Inputs[idx]);
            nw.Layer = layer;
            foreach (var wp in wps) nw.Waypoints.Add(wp);
        }
        foreach (var (idx, to, wps, layer) in outgoing)
        {
            if (idx >= fresh.Outputs.Count) continue;
            var nw = circuit.Connect(fresh.Outputs[idx], to);
            nw.Layer = layer;
            foreach (var wp in wps) nw.Waypoints.Add(wp);
        }
    }

    private void ApplyEditToTemplate(ChipTemplate tpl)
    {
        if (_macroDialog.Recipe is null) return;

        var recipe = _macroDialog.Recipe;
        var proto = tpl.Prototype;
        var oldName = tpl.Name;
        var newName = _macroDialog.Name;

        if (string.IsNullOrWhiteSpace(newName))
            return;

        proto.Rename(newName);
        proto.BodyColor = _macroDialog.BodyColor;
        proto.WireColor = _macroDialog.WireColor;
        proto.SetSize(recipe.Width, recipe.Height);

        for (int i = 0; i < recipe.InputLabels.Count && i < proto.InputLabels.Count; i++)
            proto.InputLabels[i] = recipe.InputLabels[i];
        for (int i = 0; i < recipe.OutputLabels.Count && i < proto.OutputLabels.Count; i++)
            proto.OutputLabels[i] = recipe.OutputLabels[i];

        for (int i = 0; i < recipe.InputOffsets.Count && i < proto.Inputs.Count; i++)
        {
            proto.SetOffset(proto.Inputs[i], recipe.InputOffsets[i]);
            proto.Inputs[i].Color = recipe.InputPinColors[i];
        }
        for (int i = 0; i < recipe.OutputOffsets.Count && i < proto.Outputs.Count; i++)
        {
            proto.SetOffset(proto.Outputs[i], recipe.OutputOffsets[i]);
            proto.Outputs[i].Color = recipe.OutputPinColors[i];
        }

        var newTpl = new ChipTemplate
        {
            Name = newName,
            BodyColor = proto.BodyColor,
            WireColor = proto.WireColor,
            Prototype = proto,
            CameraTarget = tpl.CameraTarget,
            CameraZoom = tpl.CameraZoom
        };

        ReplaceInList(_palette.Templates, tpl, newTpl);
        ReplaceInList(_palette.Library, tpl, newTpl);

        if (oldName != newName)
        {
            var root = _stack.Last();
            foreach (var el in root.Circuit.Elements.OfType<ChipElement>())
            {
                if (ReferenceEquals(el, proto)) continue;
                if (el.Name == oldName) el.Rename(newName);
            }
        }

        RebuildInstancesFromTemplate(newName);
    }

    private static void ReplaceInList(List<ChipTemplate> list, ChipTemplate oldTpl, ChipTemplate newTpl)
    {
        for (int i = 0; i < list.Count; i++)
            if (ReferenceEquals(list[i], oldTpl)) { list[i] = newTpl; return; }
    }
}
