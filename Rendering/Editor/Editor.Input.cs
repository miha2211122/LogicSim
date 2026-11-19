using LogicSim.Game.Core;
using Raylib_cs;
using System.Numerics;

namespace LogicSim.Game.Rendering;

public sealed partial class Editor
{
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

        if (_timingDialog.IsOpen)
        {
            _timingDialog.Update();
            if (_timingDialog.Confirmed && _timingTarget is { } timed)
            {
                BeginChange();
                if (timed is ClockElement clock) clock.SetPeriod(_timingDialog.NewValue);
                if (timed is DelayElement delay) delay.SetDelay(_timingDialog.NewValue);
                _timingTarget = null;
            }
            return;
        }

        if (_libraryDialog.IsOpen)
        {
            _libraryDialog.Update();

            if (_libraryDialog.PartSelected is { } part)
                {
                    _palette.SelectBuiltin(part.Tool);
                    _libraryDialog.Close();
                    StatusMessage = $"Selected {part.Name} — click canvas to place.";
                    _statusTimer = 2f;
                }
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

        if (_layerRename.IsOpen)
    {
        _layerRename.Update();
        if (_layerRename.Confirmed && _layerRename.Target >= 0 &&
            _layerRename.Target < Ctx.Layers.Count)
        {
            string name = _layerRename.NewName.Trim();
            if (name.Length > 0)
            {
                BeginChange();
                Ctx.Layers.Layers[_layerRename.Target].Name = name;
                StatusMessage = $"Renamed layer to {name}.";
                _statusTimer = 2f;
            }
            _layerRename.Close();
        }
        return;
    }
    if (_layerDeleteConfirm.IsOpen)
    {
        _layerDeleteConfirm.Update();
        if (_layerDeleteConfirm.Confirmed)
        {
            int target = _layerDeleteConfirm.Target;
            _layerDeleteConfirm.Close();
            PerformDeleteLayer(target);
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

        if (HandleSimulationHotkeys()) return;
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
            if (Raylib.IsKeyPressed(KeyboardKey.M)) { if (!Ctx.ReadOnly) StartMacro(camera); return; }
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
        if (_palette.MakeMacroPressed && !Ctx.ReadOnly) { StartMacro(camera); return; }

        if (HandleContextMenuRequests(ref camera)) return;
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
                    out var inserted, BeginChange, IsElementVisible, Ctx.Layers))
            {
                _selection.Clear();
                foreach (var el in inserted) _selection.Toggle(el);
                StatusMessage = $"Pasted {inserted.Count} element(s).";
            }
            else StatusMessage = "Blocked: paste overlaps existing element.";
            _statusTimer = 2f;
        }
    }

    private bool HandleContextMenuRequests(ref Camera2D camera)
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
                    EnterChipAsTemplate(tpl, ref camera);
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
                _busWidthDialog.Open("Bus Width (Bits)", current, t is BusMerger or BusSplitter ? 2 : 1);
            }
            return true;
        }
        if (_contextMenu.TimingRequested)
        {
            var t = _contextMenu.Target; _contextMenu.Close();
            if (t is not null) OpenTimingDialog(t);
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
            Element? newEl = Palette.CreateElement(_palette.SelectedTool);
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
        if (Ctx.ReadOnly) { StatusMessage = "Read-only context."; _statusTimer = 2f; return; }
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

    private bool HandleSimulationHotkeys()
    {
        bool handled = false;

        if (Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            Ctx.Circuit.Running = !Ctx.Circuit.Running;
            StatusMessage = Ctx.Circuit.Running ? "Simulation: RUN" : "Simulation: PAUSED";
            _statusTimer = 1.5f;
            handled = true;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Period))
        {
            Ctx.Circuit.StepOnce();
            StatusMessage = "Single tick";
            _statusTimer = 1.2f;
            handled = true;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.T) && !Ctx.ReadOnly)
        {
            var el = Ctx.Layout.ElementAt(_mouseWorld, VisibleElements());
            if (OpenTimingDialog(el)) handled = true;
        }
        return handled;
    }

    private bool OpenTimingDialog(Element? el)
    {
        if (el is not (ClockElement or DelayElement)) return false;
        if (!CanEdit(el)) { StatusMessage = "Layer is locked."; _statusTimer = 2f; return true; }

        _timingTarget = el;
        if (el is ClockElement clock)
            _timingDialog.Open("Clock period (ticks)", clock.PeriodTicks, 2, 600);
        else if (el is DelayElement delay)
            _timingDialog.Open("Delay (ticks)", delay.DelayTicks, 1, 120);
        return true;
    }

    private bool HandleLayerHotkeys()
    {
        bool handled = false;
        bool ctrl = Raylib.IsKeyDown(KeyboardKey.LeftControl)
                 || Raylib.IsKeyDown(KeyboardKey.RightControl);

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
        if (!ctrl && !Ctx.ReadOnly && Raylib.IsKeyPressed(KeyboardKey.V))
        {
            BeginChange();
            Ctx.Layers.ToggleVisible(Ctx.Layers.Active);
            handled = true;
        }
        if (!ctrl && !Ctx.ReadOnly && Raylib.IsKeyPressed(KeyboardKey.K))
        {
            BeginChange();
            Ctx.Layers.ToggleLocked(Ctx.Layers.Active);
            handled = true;
        }
        if (!ctrl && !Ctx.ReadOnly && Raylib.IsKeyPressed(KeyboardKey.G))
        {
            MoveElementsToActive(_selection.Selected);
            handled = true;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Delete) && ctrl && !Ctx.ReadOnly)
        {
            DeleteLayer(Ctx.Layers.Active);
            handled = true;
        }
        if (!Ctx.ReadOnly && Raylib.IsKeyPressed(KeyboardKey.F2))
        {
            OpenLayerRename(Ctx.Layers.Active);
            handled = true;
        }
        return handled;
    }
}
