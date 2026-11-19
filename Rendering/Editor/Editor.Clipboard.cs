using LogicSim.Game.Core;
using Raylib_cs;

namespace LogicSim.Game.Rendering;

public sealed partial class Editor
{
    private void CopySelection()
    {
        if (_selection.Selected.Count == 0) { StatusMessage = "Nothing selected to copy."; _statusTimer = 2f; return; }
        _clipboard.CopyFrom(Ctx.Circuit, Ctx.Layout, _selection.Selected);
        StatusMessage = $"Copied {_selection.Selected.Count} element(s).";
        _statusTimer = 2f;
    }

    private void PasteSelection()
    {
        if (Ctx.ReadOnly)
        {
            StatusMessage = "Read-only context.";
            _statusTimer = 2f;
            return;
        }

        if (!_clipboard.HasClipboard) { StatusMessage = "Clipboard is empty."; _statusTimer = 2f; return; }
        _clipboard.BeginPaste();
        StatusMessage = "Click to place, Esc / RMB to cancel.";
        _statusTimer = 3f;
    }

    private List<(int x, int y)> BatchPositions(int count, int width, int height)
    {
        const int gap = 24;
        var list = new List<(int x, int y)>();
        for (int i = 0; i < count; i++)
        {
            int x = (int)_mouseWorld.X - width / 2;
            int y = (int)_mouseWorld.Y - height / 2 + i * (height + gap);
            list.Add((x, y));
        }
        return list;
    }

    private bool BlockedByExisting(List<(int x, int y)> positions, int width, int height)
    {
        foreach (var (x, y) in positions)
        {
            var r = new Rectangle(x, y, width, height);
            foreach (var el in Ctx.Circuit.Elements)
            {
                if (!IsElementVisible(el)) continue;
                if (!Ctx.Layout.TryGet(el, out var op)) continue;
                if (Raylib.CheckCollisionRecs(r, new Rectangle(op.X, op.Y, el.Width, el.Height)))
                    return true;
            }
        }
        return false;
    }
}
