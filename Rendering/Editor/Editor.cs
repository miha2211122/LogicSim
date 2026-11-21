using LogicSim.Game.Core;
using Raylib_cs;
using System.Numerics;

namespace LogicSim.Game.Rendering;

public sealed partial class Editor
{
    private sealed class History
    {
        private const int Limit = 100;
        private readonly Stack<string> _undo = new();
        private readonly Stack<string> _redo = new();

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        public void Clear() { _undo.Clear(); _redo.Clear(); }

        public void PushUndo(string snapshot)
        {
            _undo.Push(snapshot);
            _redo.Clear();
            Trim(_undo);
        }

        public string? Undo(string current)
        {
            if (_undo.Count == 0) return null;
            _redo.Push(current);
            Trim(_redo);
            return _undo.Pop();
        }

        public string? Redo(string current)
        {
            if (_redo.Count == 0) return null;
            _undo.Push(current);
            Trim(_undo);
            return _redo.Pop();
        }

        private static void Trim(Stack<string> stack)
        {
            if (stack.Count <= Limit) return;
            var items = stack.ToArray();
            stack.Clear();
            for (int i = Math.Min(Limit, items.Length) - 1; i >= 0; i--)
                stack.Push(items[i]);
        }
    }

    private sealed class Context
    {
        public required Circuit Circuit;
        public required Layout Layout;
        public required LayerSet Layers;
        public required string Name;
        public bool ReadOnly;
        public ChipElement? OwnerChip;
        public string? TemplateName;
        public History History { get; set; } = new();
    }

    private readonly Stack<Context> _stack = new();
    private readonly Palette _palette;
    private readonly ContextMenu _contextMenu;
    private readonly MacroDialog _macroDialog;
    private readonly PinEditorDialog _pinEditor;
    private readonly WireContextMenu _wireMenu = new();
    private readonly PinContextMenu _pinMenu = new();
    private readonly RenamePinDialog _renamePinDialog = new();
    private readonly RenameLayerDialog _layerRename = new();
    private readonly ConfirmDeleteLayerDialog _layerDeleteConfirm = new();
    private int _lastLayerClickIndex = -1;
    private double _lastLayerClickTime;
    private readonly SaveDialog _saveDialog = new();
    private readonly LoadDialog _loadDialog = new();
    private readonly ColorPickerDialog _colorPicker = new();
    private readonly MacroLibraryDialog _libraryDialog = new();
    private readonly BusWidthDialog _busWidthDialog = new();
    private readonly TimingDialog _timingDialog = new();

    private readonly ClipboardController _clipboard = new();
    private readonly WireDrawer _wireDrawer = new();
    private readonly SelectionController _selection = new();
    private readonly Dictionary<ChipElement, History> _chipHistories = new();

    private Element? _colorTarget;
    private Pin? _colorPinTarget;
    private bool _colorForBody;
    private System.Drawing.Color? _recentWireColor;
    private Element? _busWidthTarget;
    private Element? _timingTarget;
    private string? _dragSnapshot;

    private bool _panningLmb;
    private Vector2 _mouseWorld;
    private MacroRecipe? _pendingRecipe;
    private bool _helpVisible;

    public string? CurrentSavePath { get; set; }
    public bool IsPaused { get; set; }

    public Palette Palette => _palette;
    public Layout Layout => Ctx.Layout;
    public LayerSet Layers => Ctx.Layers;
    public Circuit Circuit => Ctx.Circuit;
    public IReadOnlyCollection<Element> Selection => _selection.Selected;
    public IReadOnlyList<Vector2> PendingWaypoints => _wireDrawer.Waypoints;
    public ClipboardController.Data? PasteHologram => _clipboard.Hologram;
    public Vector2 PasteHologramCenter => _clipboard.HologramCenter;

    public string? StatusMessage { get; private set; }
    private float _statusTimer;

    public Vector2 MouseWorld => _mouseWorld;
    public bool IsWiring => _wireDrawer.IsActive;
    public bool SelectionAdditive => _selection.IsAdditive;

    public Vector2? WireStart =>
        _wireDrawer.FromElement is not null && _wireDrawer.FromPin is not null
            ? Ctx.Layout.PinPosition(_wireDrawer.FromElement, _wireDrawer.FromPin)
            : null;
    public Element? WireStartElement => _wireDrawer.FromElement;
    public OutputPin? WireStartPin => _wireDrawer.FromPin;

    public bool IsElementVisible(Element el) => Ctx.Layers.IsVisible(el.Layer);
    public bool CanEdit(Element el) => Ctx.Layers.CanEdit(el.Layer);
    public bool CanEditActiveLayer => Ctx.Layers.CanEdit(Ctx.Layers.Active);

    private List<Element> VisibleElements()
        => Ctx.Circuit.Elements.Where(IsElementVisible).ToList();

    private bool IsWireVisible(Wire wire)
    {
        if (!Ctx.Layers.IsVisible(wire.Layer)) return false;
        var from = Ctx.Circuit.OwnerOf(wire.From);
        var to = Ctx.Circuit.OwnerOf(wire.To);
        return from is not null && to is not null && IsElementVisible(from) && IsElementVisible(to);
    }

    public System.Drawing.Color? WirePreviewColor => _wireDrawer.FromElement?.WireColor;
    public PinHit? Hover => Ctx.Layout.PinAt(_mouseWorld, VisibleElements());
    public Wire? HoveredWire { get; private set; }

    public bool IsNested => _stack.Count > 1;
    public bool IsReadOnly => Ctx.ReadOnly;
    public string ContextName => Ctx.Name;
    public bool SelectionActive => _selection.IsBoxSelecting;
    public Vector2 SelectionStart => _selection.BoxStart;
    public bool DrawBackButton => IsNested;
    public bool HelpVisible => _helpVisible;

    public bool AnyModalOpen => _contextMenu.IsOpen
                             || _macroDialog.IsOpen || _pinEditor.IsOpen
                             || _wireMenu.IsOpen || _saveDialog.IsOpen || _loadDialog.IsOpen
                             || _colorPicker.IsOpen || _pinMenu.IsOpen || _renamePinDialog.IsOpen
                             || _libraryDialog.IsOpen || _palette.IsContextMenuOpen
                             || _busWidthDialog.IsOpen || _timingDialog.IsOpen
                             || _layerRename.IsOpen || _layerDeleteConfirm.IsOpen;

    public Editor(Circuit root, Layout rootLayout, Palette palette,
                  ContextMenu contextMenu, MacroDialog macroDialog, PinEditorDialog pinEditor)
    {
        _palette = palette;
        _contextMenu = contextMenu;
        _macroDialog = macroDialog;
        _pinEditor = pinEditor;
        _stack.Push(new Context { Circuit = root, Layout = rootLayout, Name = "root", Layers = new LayerSet() });
    }

    private Context Ctx => _stack.Peek();

    /// <summary>
    /// Тактировать текущий контекст и пересчитать все внешние цепочки проводов,
    /// чтобы изменения вложенного макроса сразу доходили до остальной схемы.
    /// </summary>
    public void StepSimulation()
    {
        Ctx.Circuit.Step();
        foreach (var ctx in _stack.Reverse())
            if (!ReferenceEquals(ctx, Ctx))
                ctx.Circuit.Settle();
    }

    /// <summary>
    /// Пересобрать соответствия внешних пинов всех открытых макросов и флаги шин
    /// после любого изменения разрядности внутри вложенного контекста.
    /// </summary>
    private void SyncChipChain()
    {
        foreach (var ctx in _stack)
        {
            if (ctx.OwnerChip is not { } chip) continue;
            chip.SyncExternalPins();
            chip.SetSize(chip.Width, chip.Height);
            chip.ResyncExternalLabels();
        }
        foreach (var ctx in _stack)
            ctx.Circuit.RefreshBusFlags();
    }

    private string Snapshot() => SaveSystem.CaptureSnapshot(Ctx.Circuit, Ctx.Layout, Ctx.Layers,
        _palette.Templates, _palette.Library);

    private void BeginChange()
    {
        if (Ctx.ReadOnly) return;
        Ctx.History.PushUndo(Snapshot());
    }

    private void RestoreSnapshot(string snapshot)
    {
        SaveSystem.ApplySnapshot(snapshot, Ctx.Circuit, Ctx.Layout, Ctx.Layers,
            _palette.Templates, _palette.Library);
        if (_palette.SelectedTemplate is { } selected &&
            !_palette.Templates.Contains(selected))
            _palette.ClearSelection();
        Ctx.OwnerChip?.RemapExternalPins();

        _selection.Clear();
        _wireDrawer.Cancel();
        _clipboard.CancelHologram();
        HoveredWire = null;
    }

    private void Undo()
    {
        if (Ctx.ReadOnly) { StatusMessage = "Read-only context."; _statusTimer = 2f; return; }

        var target = Ctx.History.Undo(Snapshot());
        if (target is null) { StatusMessage = "Nothing to undo."; _statusTimer = 2f; return; }

        RestoreSnapshot(target);
        StatusMessage = "Undo";
        _statusTimer = 1.5f;
    }

    private void Redo()
    {
        if (Ctx.ReadOnly) { StatusMessage = "Read-only context."; _statusTimer = 2f; return; }

        var target = Ctx.History.Redo(Snapshot());
        if (target is null) { StatusMessage = "Nothing to redo."; _statusTimer = 2f; return; }

        RestoreSnapshot(target);
        StatusMessage = "Redo";
        _statusTimer = 1.5f;
    }

    private void BeginElementDrag(Element element)
    {
        _dragSnapshot = Ctx.ReadOnly ? null : Snapshot();
        _selection.BeginDrag(Ctx.Layout, Ctx.Circuit, element, _mouseWorld, IsElementVisible);
    }

    private void EndElementDrag()
    {
        if (_dragSnapshot is not null)
        {
            if (Snapshot() != _dragSnapshot)
                Ctx.History.PushUndo(_dragSnapshot);
            _dragSnapshot = null;
        }
        _selection.EndDrag();
    }

    public Rectangle SaveButtonRect() => new(Raylib.GetScreenWidth() - 160, 20, 140, 26);
    public Rectangle LoadButtonRect() => new(Raylib.GetScreenWidth() - 160, 50, 140, 26);
    public Rectangle BackButtonRect() => new(Raylib.GetScreenWidth() - 160, 80, 140, 26);

    public void RequestSave() => _saveDialog.Open(SavesDir, "circuit");
    public void RequestLoad() => _loadDialog.Open(SavesDir);

    public void SaveCurrent(Camera2D camera)
    {
        if (!string.IsNullOrEmpty(CurrentSavePath))
        {
            DoSave(CurrentSavePath, camera);
        }
    }

    public Rectangle? SelectionRect()
        => _selection.IsBoxSelecting ? _selection.BoxRect(_mouseWorld) : null;

    public void DrawDialogs()
    {
        _contextMenu.Draw();
        _macroDialog.Draw();
        _pinEditor.Draw();
        _saveDialog.Draw();
        _loadDialog.Draw();
        _wireMenu.Draw();
        _pinMenu.Draw();
        _renamePinDialog.Draw();
    _layerRename.Draw();
    _layerDeleteConfirm.Draw();
        _colorPicker.Draw();
        _libraryDialog.Draw();
        _busWidthDialog.Draw();
        _timingDialog.Draw();
    }

}