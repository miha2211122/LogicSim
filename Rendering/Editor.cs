using LogicSim.Game.Core;
using Raylib_cs;
using System.Numerics;
using RColor = Raylib_cs.Color;

namespace LogicSim.Game.Rendering;

public sealed class Editor
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
    private readonly SaveDialog _saveDialog = new();
    private readonly LoadDialog _loadDialog = new();
    private readonly ColorPickerDialog _colorPicker = new();
    private readonly MacroLibraryDialog _libraryDialog = new();
    private readonly BusWidthDialog _busWidthDialog = new();

    private readonly ClipboardController _clipboard = new();
    private readonly WireDrawer _wireDrawer = new();
    private readonly SelectionController _selection = new();
    private readonly Dictionary<ChipElement, History> _chipHistories = new();

    private Element? _colorTarget;
    private Pin? _colorPinTarget;
    private bool _colorForBody;
    private System.Drawing.Color? _recentWireColor;
    private Element? _busWidthTarget;
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

    private static readonly string SavesDir = GetSavesDir();
    private static string GetSavesDir()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return System.IO.Path.Combine(appData, "LogicSim", "Saves");
    }

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
                             || _busWidthDialog.IsOpen;

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

    private string Snapshot() => SaveSystem.CaptureSnapshot(Ctx.Circuit, Ctx.Layout, Ctx.Layers);

    private void BeginChange()
    {
        if (Ctx.ReadOnly) return;
        Ctx.History.PushUndo(Snapshot());
    }

    private void RestoreSnapshot(string snapshot)
    {
        SaveSystem.ApplySnapshot(snapshot, Ctx.Circuit, Ctx.Layout, Ctx.Layers);
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
        _colorPicker.Draw();
        _libraryDialog.Draw();
        _busWidthDialog.Draw();
    }

    public void Update(ref Camera2D camera)
    {
        if (IsPaused) return;

        bool anyModalAtStart = AnyModalOpen;
        HoveredWire = null;
        var delta = Raylib.GetMouseDelta();

        if (_palette.IsContextMenuOpen)
        {
            _palette.Update();
            return;
        }

        if (_busWidthDialog.IsOpen)
        {
            _busWidthDialog.Update();
            if (_busWidthDialog.Confirmed && _busWidthTarget is { } target)
            {
                BeginChange();
                if (target is BusMerger m) m.SetBitWidth(_busWidthDialog.NewWidth);
                if (target is BusSplitter s) s.SetBitWidth(_busWidthDialog.NewWidth);
                if (target is InElement i) i.SetBitWidth(_busWidthDialog.NewWidth);
                if (target is OutElement o) o.SetBitWidth(_busWidthDialog.NewWidth);

                Ctx.Circuit.RemoveRange(new[] { target });
                Ctx.Circuit.Add(target);
                if (Ctx.Layout.TryGet(target, out var pos))
                    Ctx.Layout.Place(target, pos.X, pos.Y);

                _busWidthTarget = null;
            }
            return;
        }

        if (_libraryDialog.IsOpen)
        {
            _libraryDialog.Update();

            if (_libraryDialog.EditRequested is { } tplEdit)
            {
                _macroDialog.OpenFromTemplate(tplEdit);
                _libraryDialog.Close();
                return;
            }
            if (_libraryDialog.AttachRequested is { } tplAttach)
            {
                _palette.Library.Remove(tplAttach);
                _palette.Templates.Add(tplAttach);
                _libraryDialog.Close();
                return;
            }
            if (_libraryDialog.DeleteRequested is { } tplDelete)
            {
                _palette.Library.Remove(tplDelete);
                _libraryDialog.Close();
                return;
            }
            return;
        }

        if (_renamePinDialog.IsOpen)
        {
            _renamePinDialog.Update();
            if (_renamePinDialog.Confirmed && _renamePinDialog.Target is { } hit)
                ApplyPinRename(hit, _renamePinDialog.NewName);
            return;
        }

        if (_pinMenu.IsOpen)
        {
            _pinMenu.Update();

            if (_pinMenu.RenameRequested && _pinMenu.Target is { } h)
            {
                if (!CanEdit(h.Element)) { StatusMessage = "Layer is locked."; _statusTimer = 2f; _pinMenu.Close(); return; }
                string current = GetPinLabel(h);
                string title = $"Rename {(h.IsInput ? "input" : "output")}: {h.Element.Name}";
                _renamePinDialog.Open(title, current, h);
                return;
            }

            if (_pinMenu.PinColorRequested && _pinMenu.Target is { } hp)
            {
                if (!CanEdit(hp.Element)) { StatusMessage = "Layer is locked."; _statusTimer = 2f; _pinMenu.Close(); return; }
                _colorPinTarget = hp.Pin;
                _colorTarget = null;
                _colorForBody = false;
                var initial = hp.Pin.Color ?? hp.Element.WireColor;
                _colorPicker.Open($"Pin color: {hp.Element.Name}", initial);
                _pinMenu.Close();
                return;
            }

            if (_pinMenu.PickedWireColor is { } picked && _pinMenu.Target is { } hw)
            {
                if (!CanEdit(hw.Element)) { StatusMessage = "Layer is locked."; _statusTimer = 2f; _pinMenu.Close(); return; }
                BeginChange();
                hw.Element.WireColor = picked;
                _recentWireColor = picked;
                _pinMenu.Close();
                return;
            }

            if (_pinMenu.WireColorRequested && _pinMenu.Target is { } hc)
            {
                if (!CanEdit(hc.Element)) { StatusMessage = "Layer is locked."; _statusTimer = 2f; _pinMenu.Close(); return; }
                _colorPinTarget = null;
                _colorTarget = hc.Element;
                _colorForBody = false;
                _colorPicker.Open($"Wire color: {hc.Element.Name}", hc.Element.WireColor);
                _pinMenu.Close();
                return;
            }
            return;
        }

        if (_colorPicker.IsOpen)
        {
            _colorPicker.Update();
            if (_colorPicker.Confirmed)
            {
                if (_colorPinTarget is not null || _colorTarget is not null) BeginChange();
                if (_colorPinTarget is not null) _colorPinTarget.Color = _colorPicker.SelectedColor;
                else if (_colorTarget is not null)
                {
                    if (_colorForBody) _colorTarget.BodyColor = _colorPicker.SelectedColor;
                    else
                    {
                        _colorTarget.WireColor = _colorPicker.SelectedColor;
                        _recentWireColor = _colorPicker.SelectedColor;
                    }
                }
                _colorTarget = null;
                _colorPinTarget = null;
            }
            else if (!_colorPicker.IsOpen) { _colorTarget = null; _colorPinTarget = null; }
            return;
        }

        if (_saveDialog.IsOpen)
        {
            _saveDialog.Update();
            if (_saveDialog.Confirmed)
            {
                var safe = _saveDialog.FileName;
                if (!safe.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) safe += ".json";
                DoSave(System.IO.Path.Combine(SavesDir, safe), camera);
            }
            return;
        }

        if (_loadDialog.IsOpen)
        {
            _loadDialog.Update();
            if (_loadDialog.Confirmed && _loadDialog.SelectedPath is { } p)
                DoLoad(p, camera);
            return;
        }

        if (_macroDialog.IsOpen)
        {
            _macroDialog.Update();
            if (_macroDialog.Confirmed)
            {
                if (_macroDialog.EditingTemplate is { } tplEdit)
                {
                    ApplyEditToTemplate(tplEdit);
                    StatusMessage = $"Macro '{_macroDialog.Name}' saved. All instances updated.";
                    _statusTimer = 2f;
                }
                else CommitMacro();
            }
            return;
        }

        if (_pinEditor.IsOpen) { _pinEditor.Update(); return; }

        if (_wireMenu.IsOpen)
        {
            _wireMenu.Update();
            if (_wireMenu.DeleteRequested && _wireMenu.Target is Wire w)
            {
                if (Ctx.Layers.IsLocked(w.Layer)) { StatusMessage = "Layer is locked."; _statusTimer = 2f; }
                else
                {
                    BeginChange();
                    Ctx.Circuit.RemoveWire(w);
                }
                _wireMenu.Close();
            }
            else if (_wireMenu.LayerRequested && _wireMenu.Target is Wire lw)
            {
                if (Ctx.Layers.IsLocked(lw.Layer) || !CanEditActiveLayer)
                { StatusMessage = "Layer is locked."; _statusTimer = 2f; }
                else
                {
                    BeginChange();
                    lw.Layer = Ctx.Layers.Active;
                }
                _wireMenu.Close();
            }
            return;
        }

        if (anyModalAtStart && !AnyModalOpen && Raylib.IsKeyPressed(KeyboardKey.Escape))
            return;

        if (HandleLayerPanel()) return;
        if (HandleLayerHotkeys()) return;

        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0f)
        {
            var zm = Raylib.GetMousePosition();
            var wb = Raylib.GetScreenToWorld2D(zm, camera);
            camera.Zoom = Math.Clamp(camera.Zoom + wheel * 0.1f, 0.25f, 4f);
            var wa = Raylib.GetScreenToWorld2D(zm, camera);
            camera.Target += wb - wa;
            _mouseWorld = Raylib.GetScreenToWorld2D(zm, camera);
        }

        if (Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl))
        {
            bool ctrlShift = Raylib.IsKeyDown(KeyboardKey.LeftShift)
                          || Raylib.IsKeyDown(KeyboardKey.RightShift);
            if (Raylib.IsKeyPressed(KeyboardKey.Z)) { if (ctrlShift) Redo(); else Undo(); return; }
            if (Raylib.IsKeyPressed(KeyboardKey.Y)) { Redo(); return; }
            if (Raylib.IsKeyPressed(KeyboardKey.S)) { RequestSave(); return; }
            if (Raylib.IsKeyPressed(KeyboardKey.O)) { RequestLoad(); return; }
            if (Raylib.IsKeyPressed(KeyboardKey.C)) { CopySelection(); return; }
            if (Raylib.IsKeyPressed(KeyboardKey.V)) { PasteSelection(); return; }
            if (Raylib.IsKeyPressed(KeyboardKey.D)) { DuplicateSelection(); return; }
            if (Raylib.IsKeyPressed(KeyboardKey.A))
            {
                if (ctrlShift) _selection.Clear();
                else _selection.SelectAll(VisibleElements());
                return;
            }
            if (Raylib.IsKeyPressed(KeyboardKey.M)) { if (!Ctx.ReadOnly) StartMacro(); return; }
            if (Raylib.IsKeyPressed(KeyboardKey.L))
            {
                _libraryDialog.Open(_palette.Templates, _palette.Library);
                return;
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.F1)) { _helpVisible = !_helpVisible; return; }
        if (Raylib.IsKeyPressed(KeyboardKey.Home))
        {
            camera.Zoom = 1f;
            camera.Target = new Vector2(Raylib.GetScreenWidth() / 2f, Raylib.GetScreenHeight() / 2f);
            return;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Equal)) { ZoomAtScreenCenter(ref camera, 0.25f); return; }
        if (Raylib.IsKeyPressed(KeyboardKey.Minus)) { ZoomAtScreenCenter(ref camera, -0.25f); return; }

        if (_clipboard.Hologram is not null)
        {
            UpdatePasteHologram(ref camera, delta);
            return;
        }

        if (IsNested && Raylib.IsMouseButtonPressed(MouseButton.Left)
            && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), BackButtonRect()))
        {
            ExitChip();
            return;
        }

        if (IsNested && Raylib.IsKeyPressed(KeyboardKey.Escape) && !IsWiring)
        {
            ExitChip();
            return;
        }

        if (!IsNested && Raylib.IsKeyPressed(KeyboardKey.Escape) && !IsWiring)
        {
            IsPaused = true;
            return;
        }

        var mouseScreen = Raylib.GetMousePosition();
        _mouseWorld = Raylib.GetScreenToWorld2D(mouseScreen, camera);

        HoveredWire = _palette.IsMouseOver() ? null : FindWireAt(_mouseWorld);
        if (HoveredWire is { } hw0 && !IsWireVisible(hw0)) HoveredWire = null;

        bool mmbDown = Raylib.IsMouseButtonDown(MouseButton.Middle);
        bool lmbPan = Raylib.IsMouseButtonDown(MouseButton.Left) && _panningLmb;

        if (mmbDown || lmbPan)
        {
            camera.Target -= delta / camera.Zoom;
            _mouseWorld = Raylib.GetScreenToWorld2D(mouseScreen, camera);
            EndElementDrag();
            return;
        }

        if (Raylib.IsMouseButtonReleased(MouseButton.Middle)
            || Raylib.IsMouseButtonReleased(MouseButton.Left))
            _panningLmb = false;

        _palette.HasElements = Ctx.Circuit.Elements.Count > 0;
        _palette.CanMakeMacro = !Ctx.ReadOnly;

        if (_contextMenu.Update()) { EndElementDrag(); return; }

        bool paletteChanged = _palette.Update();
        bool overPalette = _palette.IsMouseOver();

        if (_palette.LibraryButtonPressed) { _libraryDialog.Open(_palette.Templates, _palette.Library); return; }
        if (_palette.MakeMacroPressed && !Ctx.ReadOnly) { StartMacro(); return; }

        if (HandleContextMenuRequests()) return;
        if (HandleRightClick(mouseScreen, overPalette)) return;

        HandleDelete(overPalette);

        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || (paletteChanged && overPalette))
            _wireDrawer.Cancel();

        if (overPalette) { EndElementDrag(); return; }

        bool shift = Raylib.IsKeyDown(KeyboardKey.LeftShift)
                  || Raylib.IsKeyDown(KeyboardKey.RightShift);

        HandleLeftClick(shift);

        if (_selection.IsDragging)
            _selection.UpdateDrag(Ctx.Layout, Ctx.Circuit, _mouseWorld, IsElementVisible);

        if (Raylib.IsMouseButtonReleased(MouseButton.Left))
        {
            _selection.EndBox(Ctx.Circuit, Ctx.Layout, _mouseWorld, IsElementVisible);
            EndElementDrag();
        }

        if (_statusTimer > 0f)
        {
            _statusTimer -= Raylib.GetFrameTime();
            if (_statusTimer <= 0f) StatusMessage = null;
        }
    }

    private void UpdatePasteHologram(ref Camera2D camera, Vector2 delta)
    {
        var msH = Raylib.GetMousePosition();
        _mouseWorld = Raylib.GetScreenToWorld2D(msH, camera);

        if (Raylib.IsMouseButtonDown(MouseButton.Middle))
        {
            camera.Target -= delta / camera.Zoom;
            _mouseWorld = Raylib.GetScreenToWorld2D(msH, camera);
            return;
        }

        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0f)
        {
            var wb = Raylib.GetScreenToWorld2D(msH, camera);
            camera.Zoom = Math.Clamp(camera.Zoom + wheel * 0.1f, 0.25f, 4f);
            var wa = Raylib.GetScreenToWorld2D(msH, camera);
            camera.Target += wb - wa;
            _mouseWorld = Raylib.GetScreenToWorld2D(msH, camera);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            _clipboard.CancelHologram();
            StatusMessage = "Paste canceled.";
            _statusTimer = 2f;
            return;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (_clipboard.CommitPaste(Ctx.Circuit, Ctx.Layout, _mouseWorld,
                    out var inserted, BeginChange, IsElementVisible))
            {
                _selection.Clear();
                foreach (var el in inserted) _selection.Toggle(el);
                StatusMessage = $"Pasted {inserted.Count} element(s).";
            }
            else StatusMessage = "Blocked: paste overlaps existing element.";
            _statusTimer = 2f;
        }
    }

    private bool HandleContextMenuRequests()
    {
        if (_contextMenu.ViewRequested)
        {
            var t = _contextMenu.Target; _contextMenu.Close();
            if (t is ChipElement chip) EnterChip(chip, readonlyMode: true);
            return true;
        }
        if (_contextMenu.OpenRequested)
        {
            var t = _contextMenu.Target; _contextMenu.Close();
            if (t is ChipElement chip)
            {
                var tpl = FindTemplateByName(chip.Name);
                if (tpl is not null)
                    EnterChipAsTemplate(tpl);
                else
                    EnterChip(chip, readonlyMode: false);
            }
            return true;
        }
        if (_contextMenu.BodyCustomRequested)
        {
            var t = _contextMenu.Target; _contextMenu.Close();
            if (t is not null)
            {
                if (Ctx.ReadOnly) return true;
                if (!CanEdit(t)) { StatusMessage = "Layer is locked."; _statusTimer = 2f; return true; }

                _colorTarget = t;
                _colorPinTarget = null;
                _colorForBody = true;
                _colorPicker.Open($"Body color: {t.Name}", t.BodyColor);
            }
            return true;
        }
        if (_contextMenu.BusWidthRequested)
        {
            var t = _contextMenu.Target; _contextMenu.Close();
            if (t != null)
            {
                if (!CanEdit(t)) { StatusMessage = "Layer is locked."; _statusTimer = 2f; return true; }
                _busWidthTarget = t;
                int current = 1;
                if (t is BusMerger m) current = m.BitWidth;
                else if (t is BusSplitter s) current = s.BitWidth;
                else if (t is InElement i) current = i.BitWidth;
                else if (t is OutElement o) current = o.BitWidth;
                _busWidthDialog.Open("Bus Width (Bits)", current);
            }
            return true;
        }
        if (_contextMenu.LayerRequested)
        {
            var t = _contextMenu.Target; _contextMenu.Close();
            if (t is not null)
            {
                if (!CanEdit(t)) { StatusMessage = "Layer is locked."; _statusTimer = 2f; return true; }
                MoveElementsToActive(new[] { t });
            }
            return true;
        }
        return false;
    }

    private bool HandleRightClick(Vector2 mouseScreen, bool overPalette)
    {
        if (!Raylib.IsMouseButtonPressed(MouseButton.Right) || overPalette) return false;

        if (_palette.PendingCount > 0
            || _palette.SelectedTool != Palette.Tool.None
            || _palette.SelectedTemplate is not null)
        {
            _palette.ClearSelection();
            _wireDrawer.Cancel();
            return true;
        }

        if (Ctx.ReadOnly)
        {
            _wireDrawer.Cancel();
            return true;
        }

        var visibles = VisibleElements();
        var pinHit = Ctx.Layout.PinAt(_mouseWorld, visibles);
        if (pinHit is { } ph && !_wireDrawer.IsActive)
        {
            _pinMenu.Open(ph, mouseScreen, _recentWireColor);
            return true;
        }

        var el = Ctx.Layout.ElementAt(_mouseWorld, visibles);
        if (el is not null && !_wireDrawer.IsActive)
        {
            _contextMenu.Open(el, mouseScreen);
            return true;
        }

        var wire = FindWireAt(_mouseWorld);
        if (wire is not null && !_wireDrawer.IsActive)
        {
            _wireMenu.Open(wire, mouseScreen);
            return true;
        }

        _wireDrawer.Cancel();
        return true;
    }

    private void HandleDelete(bool overPalette)
    {
        if (!Raylib.IsKeyPressed(KeyboardKey.Delete) || overPalette || Ctx.ReadOnly) return;

        var selected = _selection.Selected.Where(CanEdit).ToList();
        var target = selected.Count > 0
            ? null
            : Ctx.Layout.ElementAt(_mouseWorld, VisibleElements());
        if (selected.Count == 0 && (target is null || !CanEdit(target))) return;

        BeginChange();

        if (selected.Count > 0)
        {
            Ctx.Circuit.RemoveRange(selected);
            foreach (var el in selected) Ctx.Layout.Remove(el);
            _selection.Clear();
        }
        else if (target is not null)
        {
            Ctx.Circuit.Remove(target);
            Ctx.Layout.Remove(target);
        }
    }

    private void HandleLeftClick(bool shift)
    {
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;

        var visibles = VisibleElements();
        var hit = Ctx.Layout.PinAt(_mouseWorld, visibles);
        var elem = Ctx.Layout.ElementAt(_mouseWorld, visibles);
        bool hasTool = _palette.SelectedTool != Palette.Tool.None
                    || _palette.SelectedTemplate is not null
                    || _palette.PendingCount > 0;

        if (_wireDrawer.IsActive)
        {
            if (hit is null)
            {
                if (!IsNearAnyPin(_mouseWorld)) _wireDrawer.AddWaypoint(_mouseWorld);
                return;
            }
            if (!hit.Value.IsInput)
            {
                if (!IsNearAnyPin(_mouseWorld)) _wireDrawer.AddWaypoint(_mouseWorld);
                return;
            }
            OnLeftClick();
            return;
        }

        if (hasTool) { OnLeftClick(); return; }

        if (shift)
        {
            if (elem is not null) { _selection.Toggle(elem); return; }
            _selection.BeginBox(_mouseWorld, additive: true);
            return;
        }

        if (hit is not null) { OnLeftClick(); return; }

        if (elem is not null)
        {
            _wireDrawer.Cancel();

            if (Ctx.ReadOnly)
            {
                _selection.SetSingle(elem);
                return;
            }

            if (!CanEdit(elem)) { StatusMessage = "Layer is locked."; _statusTimer = 2f; return; }

            BeginElementDrag(elem);
            return;
        }

        _selection.Clear();
        _panningLmb = true;
    }

    private void OnLeftClick()
    {
        if (Ctx.ReadOnly) return;

        var hit = Ctx.Layout.PinAt(_mouseWorld, VisibleElements());
        if (hit is { } h)
        {
            if (!CanEdit(h.Element)) { StatusMessage = "Layer is locked."; _statusTimer = 2f; _wireDrawer.Cancel(); return; }
            if (h.IsInput)
            {
                if (h.Pin is not InputPin input) return;

                if (h.Element is InElement)
                {
                    if (_wireDrawer.IsActive)
                    {
                        StatusMessage = "IN element's pin is a button; cannot wire to it.";
                        _statusTimer = 2f;
                        _wireDrawer.Cancel();
                        return;
                    }
                    input.Toggle();
                    return;
                }

                if (_wireDrawer.IsActive)
                {
                    if (_wireDrawer.FromPin != null && _wireDrawer.FromPin.BitWidth != input.BitWidth)
                    {
                        StatusMessage = $"Cannot connect {_wireDrawer.FromPin.BitWidth}-bit wire to {input.BitWidth}-bit pin.";
                        _statusTimer = 3f;
                        _wireDrawer.Cancel();
                        return;
                    }

                    if (!CanEditActiveLayer) { StatusMessage = "Active layer is hidden or locked."; _statusTimer = 2f; _wireDrawer.Cancel(); return; }

                    BeginChange();
                    var made = _wireDrawer.TryFinish(Ctx.Circuit, h);
                    if (made is not null) made.Layer = Ctx.Layers.Active;
                    return;
                }

                if (IsDriven(input))
                {
                    StatusMessage = "Input is driven by a wire; cannot toggle.";
                    _statusTimer = 2f;
                    return;
                }

                if (h.Element is ChipElement) input.Toggle();
                return;
            }

            if (h.Pin is OutputPin output)
            {
                if (!CanEditActiveLayer) { StatusMessage = "Active layer is hidden or locked."; _statusTimer = 2f; return; }
                _wireDrawer.Begin(h.Element, output);
            }
            return;
        }

        int count = _palette.PendingCount;
        if (count > 0 && !CanEditActiveLayer)
        {
            StatusMessage = "Active layer is hidden or locked.";
            _statusTimer = 2f;
            return;
        }

        if (_palette.SelectedTemplate is { } tpl && count > 0)
        {
            int boxW = tpl.Prototype.Width;
            int boxH = tpl.Prototype.Height;

            var positions = BatchPositions(count, boxW, boxH);

            if (BlockedByExisting(positions, boxW, boxH))
            {
                StatusMessage = "Blocked: placement overlaps existing element.";
                _statusTimer = 2f;
                _palette.ClearSelection();
                return;
            }
            BeginChange();
            for (int i = 0; i < count; i++)
            {
                var el = tpl.Prototype.Clone();
                el.Layer = Ctx.Layers.Active;
                Ctx.Circuit.Add(el);
                Ctx.Layout.Place(el, positions[i].x, positions[i].y);
            }
            _palette.ClearSelection();
            return;
        }

        if (count > 0)
        {
            Element? newEl = _palette.SelectedTool switch
            {
                Palette.Tool.In => new InElement(),
                Palette.Tool.Out => new OutElement(),
                Palette.Tool.Nand => new NandElement(),
                Palette.Tool.Merge => new BusMerger(),
                Palette.Tool.Split => new BusSplitter(),
                _ => null
            };
            if (newEl is not null)
            {
                int boxW = newEl.Width;
                int boxH = newEl.Height;

                var positions = BatchPositions(count, boxW, boxH);

                if (BlockedByExisting(positions, boxW, boxH))
                {
                    StatusMessage = "Blocked: placement overlaps existing element.";
                    _statusTimer = 2f;
                    _palette.ClearSelection();
                    return;
                }
                BeginChange();
                for (int i = 0; i < count; i++)
                {
                    var el = i == 0 ? newEl : CloneForBatch(_palette.SelectedTool);
                    el.Layer = Ctx.Layers.Active;
                    Ctx.Circuit.Add(el);
                    Ctx.Layout.Place(el, positions[i].x, positions[i].y);
                }
                _palette.ClearSelection();
                return;
            }
        }

        var target = Ctx.Layout.ElementAt(_mouseWorld, VisibleElements());
        if (target is not null)
        {
            if (!CanEdit(target)) { StatusMessage = "Layer is locked."; _statusTimer = 2f; return; }
            _wireDrawer.Cancel();
            BeginElementDrag(target);
            return;
        }

        _wireDrawer.Cancel();
    }

    private void DuplicateSelection()
    {
        if (Ctx.ReadOnly) { StatusMessage = "Read-only context."; _statusTimer = 2f; return; }
        if (_selection.Selected.Count == 0)
        {
            StatusMessage = "Nothing selected to duplicate.";
            _statusTimer = 2f;
            return;
        }
        CopySelection();
        PasteSelection();
    }

    private static void ZoomAtScreenCenter(ref Camera2D camera, float delta)
    {
        var center = new Vector2(Raylib.GetScreenWidth() / 2f, Raylib.GetScreenHeight() / 2f);
        var before = Raylib.GetScreenToWorld2D(center, camera);
        camera.Zoom = Math.Clamp(camera.Zoom + delta, 0.25f, 4f);
        var after = Raylib.GetScreenToWorld2D(center, camera);
        camera.Target += before - after;
    }

    private void MoveElementsToActive(IEnumerable<Element> items)
    {
        if (!CanEditActiveLayer) { StatusMessage = "Active layer is hidden or locked."; _statusTimer = 2f; return; }

        var list = items.Where(CanEdit).ToList();
        if (list.Count == 0) { StatusMessage = "Nothing movable selected."; _statusTimer = 2f; return; }

        BeginChange();
        foreach (var el in list)
        {
            el.Layer = Ctx.Layers.Active;
            foreach (var w in Ctx.Circuit.Wires)
            {
                if (Ctx.Layers.IsLocked(w.Layer)) continue;
                if (ReferenceEquals(Ctx.Circuit.OwnerOf(w.From), el)
                    || ReferenceEquals(Ctx.Circuit.OwnerOf(w.To), el))
                    w.Layer = Ctx.Layers.Active;
            }
        }
        StatusMessage = $"Moved {list.Count} element(s) to layer {Ctx.Layers.Active + 1}.";
        _statusTimer = 2f;
    }

    private bool HandleLayerHotkeys()
    {
        bool handled = false;

        if (Raylib.IsKeyPressed((KeyboardKey)91)) // [
        {
            Ctx.Layers.SetActive(Ctx.Layers.Active - 1);
            handled = true;
        }
        if (Raylib.IsKeyPressed((KeyboardKey)93)) // ]
        {
            Ctx.Layers.SetActive(Ctx.Layers.Active + 1);
            handled = true;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.V))
        {
            BeginChange();
            Ctx.Layers.ToggleVisible(Ctx.Layers.Active);
            handled = true;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.K))
        {
            BeginChange();
            Ctx.Layers.ToggleLocked(Ctx.Layers.Active);
            handled = true;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.G))
        {
            MoveElementsToActive(_selection.Selected);
            handled = true;
        }
        return handled;
    }

    private Rectangle LayerPanelRect()
        => new(Raylib.GetScreenWidth() - 268, 118, 248, 38 + Ctx.Layers.Count * 24);

    private Rectangle LayerPrevRect()
    {
        var p = LayerPanelRect();
        return new Rectangle(p.X + 8, p.Y + 6, 26, 26);
    }

    private Rectangle LayerNextRect()
    {
        var p = LayerPanelRect();
        return new Rectangle(p.X + 38, p.Y + 6, 26, 26);
    }

    private Rectangle LayerAddRect()
    {
        var p = LayerPanelRect();
        return new Rectangle(p.X + p.Width - 34, p.Y + 6, 26, 26);
    }

    private Rectangle LayerRowRect(int i)
    {
        var p = LayerPanelRect();
        return new Rectangle(p.X + 8, p.Y + 36 + i * 24, p.Width - 16, 22);
    }

    private Rectangle LayerVisibleRect(int i)
    {
        var r = LayerRowRect(i);
        return new Rectangle(r.X + r.Width - 52, r.Y + 2, 22, 18);
    }

    private Rectangle LayerLockRect(int i)
    {
        var r = LayerRowRect(i);
        return new Rectangle(r.X + r.Width - 26, r.Y + 2, 22, 18);
    }

    private bool HandleLayerPanel()
    {
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return false;
        var m = Raylib.GetMousePosition();
        if (!Raylib.CheckCollisionPointRec(m, LayerPanelRect())) return false;

        if (Raylib.CheckCollisionPointRec(m, LayerPrevRect()))
        {
            Ctx.Layers.SetActive(Ctx.Layers.Active - 1);
            return true;
        }
        if (Raylib.CheckCollisionPointRec(m, LayerNextRect()))
        {
            Ctx.Layers.SetActive(Ctx.Layers.Active + 1);
            return true;
        }
        if (Raylib.CheckCollisionPointRec(m, LayerAddRect()))
        {
            BeginChange();
            Ctx.Layers.AddLayer();
            return true;
        }

        for (int i = 0; i < Ctx.Layers.Count; i++)
        {
            if (Raylib.CheckCollisionPointRec(m, LayerVisibleRect(i)))
            {
                BeginChange();
                Ctx.Layers.ToggleVisible(i);
                return true;
            }
            if (Raylib.CheckCollisionPointRec(m, LayerLockRect(i)))
            {
                BeginChange();
                Ctx.Layers.ToggleLocked(i);
                return true;
            }
            if (Raylib.CheckCollisionPointRec(m, LayerRowRect(i)))
            {
                Ctx.Layers.SetActive(i);
                return true;
            }
        }
        return true;
    }

    public void DrawLayerPanel()
    {
        var p = LayerPanelRect();
        Raylib.DrawRectangleRec(p, new RColor((byte)24, (byte)26, (byte)36, (byte)238));
        Raylib.DrawRectangleLinesEx(p, 1f, new RColor((byte)100, (byte)150, (byte)190, (byte)255));

        Raylib.DrawText($"LAYER {Ctx.Layers.Active + 1}/{Ctx.Layers.Count}",
            (int)p.X + 72, (int)p.Y + 9, 16, RColor.White);

        DrawSmallButton(LayerPrevRect(), "<", false);
        DrawSmallButton(LayerNextRect(), ">", false);
        DrawSmallButton(LayerAddRect(), "+", Ctx.Layers.Count >= LayerSet.MaxLayers);

        var m = Raylib.GetMousePosition();
        for (int i = 0; i < Ctx.Layers.Count; i++)
        {
            var row = LayerRowRect(i);
            bool active = i == Ctx.Layers.Active;
            var layer = Ctx.Layers.Layers[i];

            Raylib.DrawRectangleRec(row, active
                ? new RColor((byte)60, (byte)90, (byte)120, (byte)255)
                : Raylib.CheckCollisionPointRec(m, row)
                    ? new RColor((byte)42, (byte)48, (byte)64, (byte)255)
                    : new RColor((byte)32, (byte)36, (byte)48, (byte)255));
            if (active)
                Raylib.DrawRectangleLinesEx(row, 1f, new RColor((byte)150, (byte)210, (byte)255, (byte)255));

            string label = $"{i + 1}: {layer.Name}";
            Raylib.DrawText(label, (int)row.X + 6, (int)row.Y + 3, 14,
                layer.Visible ? RColor.White : new RColor((byte)120, (byte)120, (byte)130, (byte)255));

            DrawSmallButton(LayerVisibleRect(i), layer.Visible ? "V" : "-", false);
            DrawSmallButton(LayerLockRect(i), layer.Locked ? "K" : "-", false);
        }
    }

    private static void DrawSmallButton(Rectangle r, string label, bool disabled)
    {
        var m = Raylib.GetMousePosition();
        bool hover = !disabled && Raylib.CheckCollisionPointRec(m, r);
        Raylib.DrawRectangleRec(r, disabled
            ? new RColor((byte)38, (byte)40, (byte)50, (byte)255)
            : hover ? new RColor((byte)90, (byte)120, (byte)150, (byte)255)
                    : new RColor((byte)50, (byte)56, (byte)72, (byte)255));
        Raylib.DrawRectangleLinesEx(r, 1f, new RColor((byte)110, (byte)120, (byte)145, (byte)255));

        int tw = Raylib.MeasureText(label, 14);
        Raylib.DrawText(label, (int)(r.X + (r.Width - tw) / 2), (int)(r.Y + (r.Height - 14) / 2), 14,
            disabled ? RColor.Gray : RColor.White);
    }

    public void DrawHelp()
    {
        if (!_helpVisible) return;

        string[] lines =
        {
            "F1        toggle this help",
            "1..5      IN / OUT / NAND / MERGE / SPLIT (repeat = +1)",
            "0         clear active tool",
            "Ctrl+S    save      Ctrl+O    load",
            "Ctrl+C    copy      Ctrl+V    paste (click to place)",
            "Ctrl+D    duplicate selection (click to place)",
            "Ctrl+A    select all        Ctrl+Shift+A    deselect",
            "Ctrl+M    make macro        Ctrl+L    macro library",
            "Ctrl+Z    undo      Ctrl+Y / Ctrl+Shift+Z    redo",
            "Delete    delete selected / element under cursor",
            "Esc       cancel wiring / pause (root) / leave chip",
            "Shift+LMB box / additive select",
            "Wheel     zoom at cursor   + / -  zoom at center",
            "Home      reset camera     MMB or LMB drag on empty = pan",
            "RMB       pin / element / wire context menu",
            "[ / ]     previous / next active layer",
            "V         hide / show active layer",
            "K         lock / unlock active layer",
            "G         move selection to active layer",
            "+ panel   add layer / pick layer visibility and lock"
        };

        const int w = 560;
        int rowH = 26;
        int h = 70 + lines.Length * rowH + 20;
        var bounds = new Rectangle(
            (Raylib.GetScreenWidth() - w) / 2f,
            (Raylib.GetScreenHeight() - h) / 2f, w, h);

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(),
            new Color((byte)0, (byte)0, (byte)0, (byte)170));
        Raylib.DrawRectangleRec(bounds, new Color((byte)22, (byte)24, (byte)32, (byte)250));
        Raylib.DrawRectangleLinesEx(bounds, 2f, new Color((byte)110, (byte)140, (byte)190, (byte)255));

        Raylib.DrawText("LogicSim hotkeys", (int)bounds.X + 20, (int)bounds.Y + 16, 24, Color.White);

        for (int i = 0; i < lines.Length; i++)
            Raylib.DrawText(lines[i], (int)bounds.X + 20, (int)bounds.Y + 56 + i * rowH, 16,
                new Color((byte)200, (byte)215, (byte)240, (byte)255));
    }

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

    private void EnterChipAsTemplate(ChipTemplate tpl)
    {
        EnterChip(tpl.Prototype, readonlyMode: false, templateName: tpl.Name);
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

    private static Element CloneForBatch(Palette.Tool tool) => tool switch
    {
        Palette.Tool.In => new InElement(),
        Palette.Tool.Out => new OutElement(),
        Palette.Tool.Nand => new NandElement(),
        Palette.Tool.Merge => new BusMerger(),
        Palette.Tool.Split => new BusSplitter(),
        _ => throw new InvalidOperationException()
    };

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

        var incoming = new List<(int idx, OutputPin from, List<Vector2> wps)>();
        var outgoing = new List<(int idx, InputPin to, List<Vector2> wps)>();

        foreach (var w in circuit.Wires)
        {
            var ownerFrom = circuit.OwnerOf(w.From);
            var ownerTo = circuit.OwnerOf(w.To);

            if (ReferenceEquals(ownerTo, oldChip))
            {
                int idx = Layout.PinIndex(oldChip, w.To, isInput: true);
                if (idx >= 0) incoming.Add((idx, w.From, w.Waypoints.ToList()));
            }
            if (ReferenceEquals(ownerFrom, oldChip))
            {
                int idx = Layout.PinIndex(oldChip, w.From, isInput: false);
                if (idx >= 0) outgoing.Add((idx, w.To, w.Waypoints.ToList()));
            }
        }

        circuit.Remove(oldChip);
        layout.Remove(oldChip);

        var fresh = proto.Clone();
        circuit.Add(fresh);
        layout.Place(fresh, pos.X, pos.Y);

        foreach (var (idx, from, wps) in incoming)
        {
            if (idx >= fresh.Inputs.Count) continue;
            var nw = circuit.Connect(from, fresh.Inputs[idx]);
            foreach (var wp in wps) nw.Waypoints.Add(wp);
        }
        foreach (var (idx, to, wps) in outgoing)
        {
            if (idx >= fresh.Outputs.Count) continue;
            var nw = circuit.Connect(fresh.Outputs[idx], to);
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
            Prototype = proto
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

    private void DoSave(string path, Camera2D camera)
    {
        try
        {
            System.IO.Directory.CreateDirectory(SavesDir);
            var root = _stack.Last();
            SaveSystem.Save(path, root.Circuit, root.Layout,
                camera.Target, camera.Zoom,
                _palette.Templates, _palette.Library,
                System.IO.Path.GetFileNameWithoutExtension(path),
                _recentWireColor,
                root.Layers);

            CurrentSavePath = path;
            StatusMessage = $"Saved -> {path}";
        }
        catch (Exception ex) { StatusMessage = $"Save error: {ex.Message}"; }
        _statusTimer = 5f;
    }

    private void DoLoad(string path, Camera2D camera)
    {
        try
        {
            var save = SaveSystem.Load(path);
            if (save is null) { StatusMessage = $"No file: {path}"; _statusTimer = 5f; return; }

            while (_stack.Count > 1) _stack.Pop();
            var root = _stack.Peek();
            root.History.Clear();
            _chipHistories.Clear();
            _dragSnapshot = null;

            SaveSystem.Apply(save, root.Circuit, root.Layout,
                out var camTarget, out var camZoom,
                _palette.Templates, _palette.Library,
                root.Layers);

            camera.Target = camTarget;
            camera.Zoom = camZoom;

            _recentWireColor = SaveSystem.FromHexOrNull(save.RecentWireColorHex);
            _clipboard.CancelHologram();

            CurrentSavePath = path;
            StatusMessage = $"Loaded <- {path}";
        }
        catch (Exception ex) { StatusMessage = $"Load error: {ex.Message}"; }
        _statusTimer = 5f;
    }

    private void StartMacro()
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
        int h = Math.Max(80, Math.Max(recipe.Inputs.Count, recipe.Outputs.Count) * 24 + 20);

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
            _macroDialog.BodyColor, _macroDialog.WireColor);
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
            Prototype = chip.Clone()
        });

        _pendingRecipe = null;
    }
}