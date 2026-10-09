using LogicSim.Game.Core;
using Raylib_cs;
using System.Numerics;

namespace LogicSim.Game.Rendering;

public sealed class Editor
{
    private sealed class Context
    {
        public required Circuit Circuit;
        public required Layout Layout;
        public required string Name;
        public bool ReadOnly;
        public ChipElement? OwnerChip;
        public string? TemplateName; // если != null — редактируем определение макроса
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

    private readonly ClipboardController _clipboard = new();
    private readonly WireDrawer _wireDrawer = new();
    private readonly SelectionController _selection = new();

    private Element? _colorTarget;
    private Pin? _colorPinTarget;
    private bool _colorForBody;
    private System.Drawing.Color? _recentWireColor;

    private bool _panningLmb;
    private Vector2 _mouseWorld;
    private MacroRecipe? _pendingRecipe;

    public Palette Palette => _palette;
    public Layout Layout => Ctx.Layout;
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

    public System.Drawing.Color? WirePreviewColor => _wireDrawer.FromElement?.WireColor;
    public PinHit? Hover => Ctx.Layout.PinAt(_mouseWorld, Ctx.Circuit.Elements);
    public Wire? HoveredWire { get; private set; }

    public bool IsNested => _stack.Count > 1;
    public bool IsReadOnly => Ctx.ReadOnly;
    public string ContextName => Ctx.Name;
    public bool SelectionActive => _selection.IsBoxSelecting;
    public Vector2 SelectionStart => _selection.BoxStart;
    public bool DrawBackButton => IsNested;
    public bool AnyModalOpen => _macroDialog.IsOpen || _pinEditor.IsOpen
                             || _wireMenu.IsOpen || _saveDialog.IsOpen || _loadDialog.IsOpen
                             || _colorPicker.IsOpen || _pinMenu.IsOpen || _renamePinDialog.IsOpen
                             || _libraryDialog.IsOpen || _palette.IsContextMenuOpen;

    public Editor(Circuit root, Layout rootLayout, Palette palette,
                  ContextMenu contextMenu, MacroDialog macroDialog, PinEditorDialog pinEditor)
    {
        _palette = palette;
        _contextMenu = contextMenu;
        _macroDialog = macroDialog;
        _pinEditor = pinEditor;
        _stack.Push(new Context { Circuit = root, Layout = rootLayout, Name = "root" });
    }

    private Context Ctx => _stack.Peek();

    public Rectangle SaveButtonRect() => new(Raylib.GetScreenWidth() - 160, 20, 140, 26);
    public Rectangle LoadButtonRect() => new(Raylib.GetScreenWidth() - 160, 50, 140, 26);
    public Rectangle BackButtonRect() => new(Raylib.GetScreenWidth() - 160, 80, 140, 26);

    public void RequestSave() => _saveDialog.Open(SavesDir, "circuit");
    public void RequestLoad() => _loadDialog.Open(SavesDir);

    public Rectangle? SelectionRect()
        => _selection.IsBoxSelecting ? _selection.BoxRect(_mouseWorld) : null;

    public void DrawDialogs()
    {
        _saveDialog.Draw();
        _loadDialog.Draw();
        _wireMenu.Draw();
        _pinMenu.Draw();
        _renamePinDialog.Draw();
        _colorPicker.Draw();
        _libraryDialog.Draw();
    }

    public void Update(ref Camera2D camera)
    {
        HoveredWire = null;
        var delta = Raylib.GetMouseDelta();

        if (_palette.IsContextMenuOpen)
        {
            _palette.Update();
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
                string current = GetPinLabel(h);
                string title = $"Rename {(h.IsInput ? "input" : "output")}: {h.Element.Name}";
                _renamePinDialog.Open(title, current, h);
                return;
            }

            if (_pinMenu.PinColorRequested && _pinMenu.Target is { } hp)
            {
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
                hw.Element.WireColor = picked;
                _recentWireColor = picked;
                _pinMenu.Close();
                return;
            }

            if (_pinMenu.WireColorRequested && _pinMenu.Target is { } hc)
            {
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
                Ctx.Circuit.RemoveWire(w);
                _wireMenu.Close();
            }
            return;
        }

        if (Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl))
        {
            if (Raylib.IsKeyPressed(KeyboardKey.S)) { RequestSave(); return; }
            if (Raylib.IsKeyPressed(KeyboardKey.O)) { RequestLoad(); return; }
            if (Raylib.IsKeyPressed(KeyboardKey.C)) { CopySelection(); return; }
            if (Raylib.IsKeyPressed(KeyboardKey.V)) { PasteSelection(); return; }
        }

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

        var mouseScreen = Raylib.GetMousePosition();
        _mouseWorld = Raylib.GetScreenToWorld2D(mouseScreen, camera);

        HoveredWire = _palette.IsMouseOver() ? null : FindWireAt(_mouseWorld);

        bool mmbDown = Raylib.IsMouseButtonDown(MouseButton.Middle);
        bool lmbPan = Raylib.IsMouseButtonDown(MouseButton.Left) && _panningLmb;

        if (mmbDown || lmbPan)
        {
            camera.Target -= delta / camera.Zoom;
            _mouseWorld = Raylib.GetScreenToWorld2D(mouseScreen, camera);
            _selection.EndDrag();
            return;
        }

        if (Raylib.IsMouseButtonReleased(MouseButton.Middle)
            || Raylib.IsMouseButtonReleased(MouseButton.Left))
            _panningLmb = false;

        _palette.HasElements = Ctx.Circuit.Elements.Count > 0;
        _palette.CanMakeMacro = !Ctx.ReadOnly;

        if (_contextMenu.Update()) { _selection.EndDrag(); return; }

        bool paletteChanged = _palette.Update();
        bool overPalette = _palette.IsMouseOver();

        if (_palette.LibraryButtonPressed) { _libraryDialog.Open(_palette.Templates, _palette.Library); return; }
        if (_palette.MakeMacroPressed && !Ctx.ReadOnly) { StartMacro(); return; }

        if (HandleContextMenuRequests()) return;
        if (HandleRightClick(mouseScreen, overPalette)) return;

        HandleDelete(overPalette);

        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || (paletteChanged && overPalette))
            _wireDrawer.Cancel();

        if (overPalette) { _selection.EndDrag(); return; }

        bool shift = Raylib.IsKeyDown(KeyboardKey.LeftShift)
                  || Raylib.IsKeyDown(KeyboardKey.RightShift);

        HandleLeftClick(shift);

        if (_selection.IsDragging)
            _selection.UpdateDrag(Ctx.Layout, Ctx.Circuit, _mouseWorld);

        if (Raylib.IsMouseButtonReleased(MouseButton.Left))
        {
            _selection.EndBox(Ctx.Circuit, Ctx.Layout, _mouseWorld);
            _selection.EndDrag();
        }

        if (_statusTimer > 0f)
        {
            _statusTimer -= Raylib.GetFrameTime();
            if (_statusTimer <= 0f) StatusMessage = null;
        }
    }

    // ─── Paste hologram ───

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
            if (_clipboard.CommitPaste(Ctx.Circuit, Ctx.Layout, _mouseWorld, out var inserted))
            {
                _selection.Clear();
                foreach (var el in inserted) _selection.Toggle(el);
                StatusMessage = $"Pasted {inserted.Count} element(s).";
            }
            else StatusMessage = "Blocked: paste overlaps existing element.";
            _statusTimer = 2f;
        }
    }

    // ─── Context menu ───

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
                    EnterChip(chip, readonlyMode: false); // fallback: старое поведение
            }
            return true;
        }
        if (_contextMenu.BodyCustomRequested)
        {
            var t = _contextMenu.Target; _contextMenu.Close();
            if (t is not null)
            {
                _colorTarget = t;
                _colorPinTarget = null;
                _colorForBody = true;
                _colorPicker.Open($"Body color: {t.Name}", t.BodyColor);
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

        var pinHit = Ctx.Layout.PinAt(_mouseWorld, Ctx.Circuit.Elements);
        if (pinHit is { } ph && !_wireDrawer.IsActive && !Ctx.ReadOnly)
        {
            _pinMenu.Open(ph, mouseScreen, _recentWireColor);
            return true;
        }

        var el = Ctx.Layout.ElementAt(_mouseWorld, Ctx.Circuit.Elements);
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

        if (_selection.Selected.Count > 0)
        {
            Ctx.Circuit.RemoveRange(_selection.Selected);
            foreach (var el in _selection.Selected) Ctx.Layout.Remove(el);
            _selection.Clear();
        }
        else
        {
            var target = Ctx.Layout.ElementAt(_mouseWorld, Ctx.Circuit.Elements);
            if (target is not null) { Ctx.Circuit.Remove(target); Ctx.Layout.Remove(target); }
        }
    }

    private void HandleLeftClick(bool shift)
    {
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;

        var hit = Ctx.Layout.PinAt(_mouseWorld, Ctx.Circuit.Elements);
        var elem = Ctx.Layout.ElementAt(_mouseWorld, Ctx.Circuit.Elements);
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
            _selection.BeginDrag(Ctx.Layout, Ctx.Circuit, elem, _mouseWorld);
            return;
        }

        _selection.Clear();
        _panningLmb = true;
    }

    private void OnLeftClick()
    {
        if (Ctx.ReadOnly) return;

        var hit = Ctx.Layout.PinAt(_mouseWorld, Ctx.Circuit.Elements);
        if (hit is { } h)
        {
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

                if (_wireDrawer.IsActive) { _wireDrawer.TryFinish(Ctx.Circuit, h); return; }

                if (IsDriven(input))
                {
                    StatusMessage = "Input is driven by a wire; cannot toggle.";
                    _statusTimer = 2f;
                    return;
                }

                if (h.Element is ChipElement) input.Toggle();
                return;
            }

            if (h.Pin is OutputPin output) _wireDrawer.Begin(h.Element, output);
            return;
        }

        int count = _palette.PendingCount;

        if (_palette.SelectedTemplate is { } tpl && count > 0)
        {
            var positions = BatchPositions(count);
            if (BlockedByExisting(positions))
            {
                StatusMessage = "Blocked: placement overlaps existing element.";
                _statusTimer = 2f;
                _palette.ClearSelection();
                return;
            }
            for (int i = 0; i < count; i++)
            {
                var el = tpl.Prototype.Clone();
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
                _ => null
            };
            if (newEl is not null)
            {
                var positions = BatchPositions(count);
                if (BlockedByExisting(positions))
                {
                    StatusMessage = "Blocked: placement overlaps existing element.";
                    _statusTimer = 2f;
                    _palette.ClearSelection();
                    return;
                }
                for (int i = 0; i < count; i++)
                {
                    var el = i == 0 ? newEl : CloneForBatch(_palette.SelectedTool);
                    Ctx.Circuit.Add(el);
                    Ctx.Layout.Place(el, positions[i].x, positions[i].y);
                }
                _palette.ClearSelection();
                return;
            }
        }

        var target = Ctx.Layout.ElementAt(_mouseWorld, Ctx.Circuit.Elements);
        if (target is not null)
        {
            _wireDrawer.Cancel();
            _selection.BeginDrag(Ctx.Layout, Ctx.Circuit, target, _mouseWorld);
            return;
        }

        _wireDrawer.Cancel();
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
        if (!_clipboard.HasClipboard) { StatusMessage = "Clipboard is empty."; _statusTimer = 2f; return; }
        _clipboard.BeginPaste();
        StatusMessage = "Click to place, Esc / RMB to cancel.";
        _statusTimer = 3f;
    }

    private List<(int x, int y)> BatchPositions(int count)
    {
        const int gap = 24;
        var list = new List<(int x, int y)>();
        for (int i = 0; i < count; i++)
        {
            int x = (int)_mouseWorld.X - Element.DefaultWidth / 2;
            int y = (int)_mouseWorld.Y - Element.DefaultHeight / 2 + i * (Element.DefaultHeight + gap);
            list.Add((x, y));
        }
        return list;
    }

    private bool BlockedByExisting(List<(int x, int y)> positions)
    {
        foreach (var (x, y) in positions)
        {
            var r = new Rectangle(x, y, Element.DefaultWidth, Element.DefaultHeight);
            foreach (var el in Ctx.Circuit.Elements)
            {
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
            if (hit.Element is OutElement oe && ReferenceEquals(oe.Out, hit.Pin))
                return oe.OutputLabels.Count > 0 ? oe.OutputLabels[0] : "Y";
            int idx = Layout.PinIndex(hit.Element, hit.Pin, isInput: false);
            if (idx >= 0 && idx < hit.Element.OutputLabels.Count) return hit.Element.OutputLabels[idx];
        }
        return "";
    }

    private void ApplyPinRename(PinHit hit, string newName)
    {
        if (string.IsNullOrEmpty(newName)) return;

        if (hit.IsInput)
        {
            int idx = Layout.PinIndex(hit.Element, hit.Pin, isInput: true);
            if (idx >= 0 && idx < hit.Element.InputLabels.Count)
                hit.Element.InputLabels[idx] = newName;
        }
        else
        {
            if (hit.Element is OutElement oe && ReferenceEquals(oe.Out, hit.Pin))
            {
                if (oe.OutputLabels.Count == 0) oe.OutputLabels.Add("Y");
                oe.OutputLabels[0] = newName;
            }
            else
            {
                int idx = Layout.PinIndex(hit.Element, hit.Pin, isInput: false);
                if (idx >= 0 && idx < hit.Element.OutputLabels.Count)
                    hit.Element.OutputLabels[idx] = newName;
            }
        }

        Ctx.OwnerChip?.ResyncExternalLabels();
    }

    private Wire? FindWireAt(Vector2 p)
    {
        const float threshold = 12f;

        foreach (var wire in Ctx.Circuit.Wires)
        {
            var owner1 = Ctx.Circuit.Elements.FirstOrDefault(e => e.Outputs.Contains(wire.From));
            var owner2 = Ctx.Circuit.Elements.FirstOrDefault(e => e.Inputs.Contains(wire.To));
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

    // ─── Chip context ───

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
            Name = chip.Name,
            ReadOnly = readonlyMode,
            OwnerChip = chip,
            TemplateName = templateName
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

        if (top.OwnerChip is not null)
        {
            foreach (var el in top.OwnerChip.Inner)
                if (top.Layout.TryGet(el, out var p))
                    top.OwnerChip.SetInnerPosition(el, p.X, p.Y);

            if (!top.ReadOnly)
            {
                top.OwnerChip.InnerWires.Clear();
                top.OwnerChip.InnerWires.AddRange(top.Circuit.WiresList);
                top.OwnerChip.ResyncExternalLabels();
            }
        }

        // Если редактировали определение макроса — пересобираем все инстансы.
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
        _ => throw new InvalidOperationException()
    };

    private bool IsDriven(InputPin pin) => Ctx.Circuit.Wires.Any(w => ReferenceEquals(w.To, pin));

    // ─── Templates / Library ───

    private ChipTemplate? FindTemplateByName(string name)
    {
        foreach (var t in _palette.Templates) if (t.Name == name) return t;
        foreach (var t in _palette.Library) if (t.Name == name) return t;
        return null;
    }

    /// <summary>
    /// Заменяет все ChipElement с именем templateName в **корневой** схеме
    /// на свежие клоны Prototype, сохраняя позицию и перевешивая провода по индексам пинов.
    /// </summary>
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

        // Снимок входящих и исходящих проводов.
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

        // Удаляем старый инстанс (это автоматом снимет все его провода).
        circuit.Remove(oldChip);
        layout.Remove(oldChip);

        // Создаём свежий клон из прототипа.
        var fresh = proto.Clone();
        circuit.Add(fresh);
        layout.Place(fresh, pos.X, pos.Y);

        // Перевешиваем провода по индексам.
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

        // Обновляем ChipTemplate в списке (Name/Color — init-only, поэтому новый).
        var newTpl = new ChipTemplate
        {
            Name = newName,
            BodyColor = proto.BodyColor,
            WireColor = proto.WireColor,
            Prototype = proto
        };

        ReplaceInList(_palette.Templates, tpl, newTpl);
        ReplaceInList(_palette.Library, tpl, newTpl);

        // Переименовываем инстансы, если имя изменилось.
        if (oldName != newName)
        {
            var root = _stack.Last();
            foreach (var el in root.Circuit.Elements.OfType<ChipElement>())
            {
                if (ReferenceEquals(el, proto)) continue;
                if (el.Name == oldName) el.Rename(newName);
            }
        }

        // Пересобираем инстансы из обновлённого Prototype.
        RebuildInstancesFromTemplate(newName);
    }

    private static void ReplaceInList(List<ChipTemplate> list, ChipTemplate oldTpl, ChipTemplate newTpl)
    {
        for (int i = 0; i < list.Count; i++)
            if (ReferenceEquals(list[i], oldTpl)) { list[i] = newTpl; return; }
    }

    // ─── Save / Load ───

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
                _recentWireColor);
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

            SaveSystem.Apply(save, root.Circuit, root.Layout,
                out var camTarget, out var camZoom,
                _palette.Templates, _palette.Library);

            camera.Target = camTarget;
            camera.Zoom = camZoom;

            _recentWireColor = SaveSystem.FromHexOrNull(save.RecentWireColorHex);
            _clipboard.CancelHologram();

            StatusMessage = $"Loaded <- {path}";
        }
        catch (Exception ex) { StatusMessage = $"Load error: {ex.Message}"; }
        _statusTimer = 5f;
    }

    // ─── MAKE MACRO ───

    private void StartMacro()
    {
        if (Ctx.Circuit.Elements.Count == 0) return;

        // Если мы уже внутри макроса (открыт через Open / Edit) — открываем
        // диалог редактирования существующего шаблона, а не создаём новый.
        if (Ctx.TemplateName is { } tname)
        {
            var existing = FindTemplateByName(tname);
            if (existing is not null)
            {
                _macroDialog.OpenFromTemplate(existing);
                return;
            }
        }

        // Иначе — обычное создание нового макроса из выделения.
        var recipe = MacroBuilder.Prepare(Ctx.Circuit, Ctx.Circuit.Elements.ToList());
        if (recipe is null) return;

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
        var recipe = _macroDialog.Recipe;

        var chip = MacroBuilder.Commit(Ctx.Circuit, recipe, _macroDialog.Name,
            _macroDialog.BodyColor, _macroDialog.WireColor);

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