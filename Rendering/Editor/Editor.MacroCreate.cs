using LogicSim.Game.Core;
using Raylib_cs;
using System.Numerics;

namespace LogicSim.Game.Rendering;

public sealed partial class Editor
{
    private void StartMacro(Camera2D camera)
    {
        if (Ctx.Circuit.Elements.Count == 0) return;

        if (Ctx.TemplateName is { } tname)
        {
            var existing = FindTemplateByName(tname);
            if (existing is not null)
            {
                _macroDialog.OpenFromTemplate(existing);
                return;
            }
        }

        var recipe = MacroBuilder.Prepare(Ctx.Circuit, Ctx.Circuit.Elements.ToList());
        if (recipe is null) return;

        int maxLabelW = 0;
        foreach (var lbl in recipe.InputLabels) maxLabelW = Math.Max(maxLabelW, Raylib.MeasureText(lbl, 14));
        foreach (var lbl in recipe.OutputLabels) maxLabelW = Math.Max(maxLabelW, Raylib.MeasureText(lbl, 14));

        int w = Math.Max(130, maxLabelW * 2 + 60);
        int h = ChipElement.MinHeightFor(
            recipe.Inputs.Select(x => (Pin)x.ext).ToList(),
            recipe.Outputs.Select(x => (Pin)x.ext).ToList());

        recipe.CameraTarget = camera.Target;
        recipe.CameraZoom = camera.Zoom;

        recipe.Width = w;
        recipe.Height = h;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (var el in Ctx.Circuit.Elements)
        {
            if (!Ctx.Layout.TryGet(el, out var p)) continue;
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X + el.Width);
            maxY = Math.Max(maxY, p.Y + el.Height);
        }
        recipe.Center = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);

        _pendingRecipe = recipe;
        _macroDialog.Open(recipe, MacroDialog.NextNumber());
    }

    private void CommitMacro()
    {
        if (_pendingRecipe is null || _macroDialog.Recipe is null) return;
        if (string.IsNullOrWhiteSpace(_macroDialog.Name)) return;

        var recipe = _macroDialog.Recipe;

        var chip = MacroBuilder.Commit(Ctx.Circuit, recipe, _macroDialog.Name,
            _macroDialog.BodyColor, _macroDialog.WireColor, Ctx.Layers);
        chip.Layer = Ctx.Layers.Active;

        foreach (var el in recipe.Inner)
            if (Ctx.Layout.TryGet(el, out var p))
                chip.SetInnerPosition(el, p.X, p.Y);

        foreach (var el in recipe.SourceElements) Ctx.Layout.Remove(el);

        Ctx.Layout.Place(chip,
            (int)recipe.Center.X - chip.Width / 2,
            (int)recipe.Center.Y - chip.Height / 2);

        _palette.Templates.Add(new ChipTemplate
        {
            Name = chip.Name,
            BodyColor = chip.BodyColor,
            WireColor = chip.WireColor,
            Prototype = chip.Clone(),
            CameraTarget = recipe.CameraTarget,
            CameraZoom = recipe.CameraZoom
        });

        _pendingRecipe = null;
    }
}
