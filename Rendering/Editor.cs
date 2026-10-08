using System.Numerics;
using LogicSim.Game.Core;
using Raylib_cs;

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
    }

    private readonly Stack<Context> _stack = new();
    private readonly Palette _palette;
    private readonly ContextMenu _contextMenu;
    private readonly MacroDialog _macroDialog;
    private readonly PinEditorDialog _pinEditor;
    private readonly WireContextMenu _wireMenu = new();
    private readonly SaveDialog _saveDialog = new();
    private readonly LoadDialog _loadDialog = new();

    private readonly HashSet<Element> _selection = new();
    private readonly Dictionary<Element, Vector2> _dragOffsets = new();

    private Vector2 _selectionStart;
    private bool _selecting;
    private bool _selectingAdditive;
    private bool _dragging;
    private bool _panningLmb;
    private Vector2 _mouseWorld;

    private Element? _wireFromElement;
    private OutputPin? _wireFromPin;
    private readonly List<Vector2> _pendingWaypoints = new();

    private MacroRecipe? _pendingRecipe;

    public Palette Palette => _palette;
    public Layout Layout => Ctx.Layout;
    public Circuit Circuit => Ctx.Circuit;
    public IReadOnlyCollection<Element> Selection => _selection;
    public IReadOnlyList<Vector2> PendingWaypoints => _pendingWaypoints;

    private static readonly string SavesDir =
        System.IO.Path.Combine(AppContext.BaseDirectory, "Saves");

    public string? StatusMessage { get; private set; }
    private float _statusTimer;

    public Vector2 MouseWorld => _mouseWorld;
    public bool IsWiring => _wireFromPin is not null;
    public bool SelectionAdditive => _selectingAdditive;

    public Vector2? WireStart =>
        _wireFromElement is not null && _wireFromPin is not null
            ? Ctx.Layout.PinPosition(_wireFromElement, _wireFromPin)
            : null;

    public System.Drawing.Color? WirePreviewColor => _wireFromElement?.WireColor;
    public PinHit? Hover => Ctx.Layout.PinAt(_mouseWorld, Ctx.Circuit.Elements);

    public bool IsNested => _stack.Count > 1;
    public bool IsReadOnly => Ctx.ReadOnly;
    public string ContextName => Ctx.Name;
    public bool SelectionActive => _selecting;
    public Vector2 SelectionStart => _selectionStart;
    public bool DrawBackButton => IsNested;
    public bool AnyModalOpen => _macroDialog.IsOpen || _pinEditor.IsOpen
                             || _wireMenu.IsOpen || _saveDialog.IsOpen || _loadDialog.IsOpen;

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
    {
        if (!_selecting) return null;
        float x = MathF.Min(_selectionStart.X, _mouseWorld.X);
        float y = MathF.Min(_selectionStart.Y, _mouseWorld.Y);
        return new Rectangle(x, y,
            MathF.Abs(_selectionStart.X - _mouseWorld.X),
            MathF.Abs(_selectionStart.Y - _mouseWorld.Y));
    }

    public void DrawDialogs()
    {
        _saveDialog.Draw();
        _loadDialog.Draw();
        _wireMenu.Draw();
    }

    public void Update(ref Camera2D camera)
    {
        if (_saveDialog.IsOpen)
        {
            _saveDialog.Update();
            if (_saveDialog.Confirmed)
            {
                var safe = _saveDialog.FileName;
                if (!safe.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) safe += ".json";
                var path = System.IO.Path.Combine(SavesDir, safe);
                DoSave(path, camera);
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
            if (_macroDialog.Confirmed) CommitMacro();
            return;
        }

        if (_pinEditor.IsOpen) { _pinEditor.Update(); return; }

        // ─── Wire context menu ───
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

        // Хоткеи
        if (Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl))
        {
            if (Raylib.IsKeyPressed(KeyboardKey.S)) { RequestSave(); return; }
            if (Raylib.IsKeyPressed(KeyboardKey.O)) { RequestLoad(); return; }
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

        var delta = Raylib.GetMouseDelta();
        var mouseScreen = Raylib.GetMousePosition();
        _mouseWorld = Raylib.GetScreenToWorld2D(mouseScreen, camera);

        // ─── Панорама ───
        bool mmbDown = Raylib.IsMouseButtonDown(MouseButton.Middle);
        bool lmbPan = Raylib.IsMouseButtonDown(MouseButton.Left) && _panningLmb;

        if (mmbDown || lmbPan)
        {
            camera.Target -= delta / camera.Zoom;
            _mouseWorld = Raylib.GetScreenToWorld2D(mouseScreen, camera);
            _dragging = false;
            _selecting = false;
            return;
        }

        if (Raylib.IsMouseButtonReleased(MouseButton.Middle)
            || Raylib.IsMouseButtonReleased(MouseButton.Left))
            _panningLmb = false;

        _palette.HasElements = Ctx.Circuit.Elements.Count > 0;
        _palette.CanMakeMacro = !Ctx.ReadOnly;

        if (_contextMenu.Update()) { _dragging = false; return; }

        bool paletteChanged = _palette.Update();
        bool overPalette = _palette.IsMouseOver();

        if (_palette.MakeMacroPressed && !Ctx.ReadOnly) { StartMacro(); return; }

        if (_contextMenu.ViewRequested)
        {
            var t = _contextMenu.Target; _contextMenu.Close();
            if (t is ChipElement chip) EnterChip(chip, readonlyMode: true);
            return;
        }
        if (_contextMenu.OpenRequested)
        {
            var t = _contextMenu.Target; _contextMenu.Close();
            if (t is ChipElement chip) EnterChip(chip, readonlyMode: false);
            return;
        }
        if (_contextMenu.PinsRequested)
        {
            var t = _contextMenu.Target; _contextMenu.Close();
            if (t is not null && !Ctx.ReadOnly) _pinEditor.Open(t);
            return;
        }

        // ─── ПКМ ───
        if (Raylib.IsMouseButtonPressed(MouseButton.Right) && !overPalette)
        {
            var el = Ctx.Layout.ElementAt(_mouseWorld, Ctx.Circuit.Elements);
            if (el is not null && _wireFromPin is null)
            {
                _contextMenu.Open(el, mouseScreen);
                return;
            }

            var wire = FindWireAt(_mouseWorld);
            if (wire is not null && _wireFromPin is null)
            {
                _wireMenu.Open(wire, mouseScreen);
                return;
            }

            CancelWire();
        }

        // ─── Delete ───
        if (Raylib.IsKeyPressed(KeyboardKey.Delete) && !overPalette && !Ctx.ReadOnly)
        {
            if (_selection.Count > 0)
            {
                Ctx.Circuit.RemoveRange(_selection);
                foreach (var el in _selection) Ctx.Layout.Remove(el);
                _selection.Clear();
            }
            else
            {
                var target = Ctx.Layout.ElementAt(_mouseWorld, Ctx.Circuit.Elements);
                if (target is not null) { Ctx.Circuit.Remove(target); Ctx.Layout.Remove(target); }
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || (paletteChanged && overPalette))
            CancelWire();

        if (overPalette) { _dragging = false; return; }

        bool shift = Raylib.IsKeyDown(KeyboardKey.LeftShift)
                  || Raylib.IsKeyDown(KeyboardKey.RightShift);

        // ═══ LMB press ═══
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            var hit = Ctx.Layout.PinAt(_mouseWorld, Ctx.Circuit.Elements);
            var elem = Ctx.Layout.ElementAt(_mouseWorld, Ctx.Circuit.Elements);
            bool hasTool = _palette.SelectedTool != Palette.Tool.None
                        || _palette.SelectedTemplate is not null
                        || _palette.PendingCount > 0;

            // 1. Завершение/продолжение провода
            if (_wireFromPin is not null)
            {
                if (hit is null) { _pendingWaypoints.Add(_mouseWorld); return; }
                OnLeftClick();
                return;
            }

            // 2. Активен инструмент — ставим
            if (hasTool)
            {
                OnLeftClick();
                return;
            }

            // 3. Shift — выделение (toggle или рамка)
            if (shift)
            {
                if (elem is not null)
                {
                    if (_selection.Contains(elem)) _selection.Remove(elem);
                    else _selection.Add(elem);
                    return;
                }
                _selecting = true;
                _selectionStart = _mouseWorld;
                _selectingAdditive = true;
                return;
            }

            // 4. Клик по пину — toggle входа или начало провода
            if (hit is not null)
            {
                OnLeftClick();
                return;
            }

            // 5. Клик по телу элемента — выделяем его одного и тянем
            if (elem is not null)
            {
                _selection.Clear();
                _selection.Add(elem);
                CancelWire();
                _dragging = true;
                _dragOffsets.Clear();
                if (Ctx.Layout.TryGet(elem, out var p))
                    _dragOffsets[elem] = new Vector2(p.X, p.Y);
                return;
            }

            // 6. Пусто — сбрасываем выделение, панорама
            _selection.Clear();
            _panningLmb = true;
        }

        // ─── LMB down — движение ───
        if (Raylib.IsMouseButtonDown(MouseButton.Left) && _dragging && !Ctx.ReadOnly)
        {
            var md = delta / camera.Zoom;
            foreach (var kv in _dragOffsets)
            {
                _dragOffsets[kv.Key] += md;
                Ctx.Layout.Place(kv.Key, (int)kv.Value.X, (int)kv.Value.Y);
            }
        }

        // ─── LMB release ───
        if (Raylib.IsMouseButtonReleased(MouseButton.Left))
        {
            if (_selecting)
            {
                var rect = SelectionRect() ?? new Rectangle();
                if (!_selectingAdditive) _selection.Clear();

                foreach (var el in Ctx.Circuit.Elements)
                {
                    if (!Ctx.Layout.TryGet(el, out var p)) continue;
                    if (Raylib.CheckCollisionRecs(rect, new Rectangle(p.X, p.Y, Layout.W, Layout.H)))
                        _selection.Add(el);
                }
                _selecting = false;
                _selectingAdditive = false;
            }
            _dragging = false;
            _dragOffsets.Clear();
        }

        if (_statusTimer > 0f)
        {
            _statusTimer -= Raylib.GetFrameTime();
            if (_statusTimer <= 0f) StatusMessage = null;
        }
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

    private void EnterChip(ChipElement chip, bool readonlyMode)
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
            OwnerChip = chip
        });

        _selection.Clear();
        CancelWire();
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
            }
        }
        _selection.Clear();
        CancelWire();
    }

    private void OnLeftClick()
    {
        if (Ctx.ReadOnly) return;

        var hit = Ctx.Layout.PinAt(_mouseWorld, Ctx.Circuit.Elements);
        if (hit is { } h)
        {
            if (h.IsInput)
            {
                var input = h.Pin as InputPin;
                if (input is null) return;
                if (_wireFromPin is not null)
                {
                    var wire = Ctx.Circuit.Connect(_wireFromPin, input);
                    foreach (var wp in _pendingWaypoints) wire.Waypoints.Add(wp);
                    CancelWire();
                    return;
                }
                if (!IsDriven(input) && (h.Element is InElement || h.Element is ChipElement))
                    input.Toggle();
                return;
            }

            var output = h.Pin as OutputPin;
            if (output is null) return;
            _wireFromElement = h.Element;
            _wireFromPin = output;
            _pendingWaypoints.Clear();
            return;
        }

        int count = _palette.PendingCount;

        if (_palette.SelectedTemplate is { } tpl && count > 0)
        {
            for (int i = 0; i < count; i++) PlaceAt(tpl.Prototype.Clone(), i);
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
                for (int i = 0; i < count; i++)
                    PlaceAt(i == 0 ? newEl : CloneForBatch(_palette.SelectedTool), i);
                _palette.ClearSelection();
                return;
            }
        }

        var target = Ctx.Layout.ElementAt(_mouseWorld, Ctx.Circuit.Elements);
        if (target is not null)
        {
            CancelWire();
            _dragging = true;
            _dragOffsets.Clear();
            if (_selection.Contains(target))
            {
                foreach (var el in _selection)
                    if (Ctx.Layout.TryGet(el, out var p))
                        _dragOffsets[el] = new Vector2(p.X, p.Y);
            }
            else if (Ctx.Layout.TryGet(target, out var p2))
            {
                _dragOffsets[target] = new Vector2(p2.X, p2.Y);
            }
            return;
        }

        CancelWire();
    }

    private static Element CloneForBatch(Palette.Tool tool) => tool switch
    {
        Palette.Tool.In => new InElement(),
        Palette.Tool.Out => new OutElement(),
        Palette.Tool.Nand => new NandElement(),
        _ => throw new InvalidOperationException()
    };

    private void PlaceAt(Element el, int index)
    {
        const int gap = 24;
        int x = (int)_mouseWorld.X - Layout.W / 2;
        int y = (int)_mouseWorld.Y - Layout.H / 2 + index * (Layout.H + gap);
        Ctx.Circuit.Add(el);
        Ctx.Layout.Place(el, x, y);
    }

    private bool IsDriven(InputPin pin) => Ctx.Circuit.Wires.Any(w => ReferenceEquals(w.To, pin));

    private void CancelWire()
    {
        _wireFromElement = null;
        _wireFromPin = null;
        _pendingWaypoints.Clear();
    }

    private void DoSave(string path, Camera2D camera)
    {
        try
        {
            System.IO.Directory.CreateDirectory(SavesDir);
            var root = _stack.Last();
            SaveSystem.Save(path, root.Circuit, root.Layout,
                camera.Target, camera.Zoom, _palette.Templates,
                System.IO.Path.GetFileNameWithoutExtension(path));
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
                out var camTarget, out var camZoom, _palette.Templates);

            camera.Target = camTarget;
            camera.Zoom = camZoom;
            StatusMessage = $"Loaded <- {path}";
        }
        catch (Exception ex) { StatusMessage = $"Load error: {ex.Message}"; }
        _statusTimer = 5f;
    }

    private void StartMacro()
    {
        if (Ctx.Circuit.Elements.Count == 0) return;
        var recipe = MacroBuilder.Prepare(Ctx.Circuit, Ctx.Circuit.Elements.ToList());
        if (recipe is null) return;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (var el in Ctx.Circuit.Elements)
        {
            if (!Ctx.Layout.TryGet(el, out var p)) continue;
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X + Layout.W);
            maxY = Math.Max(maxY, p.Y + Layout.H);
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
            (int)recipe.Center.X - Layout.W / 2,
            (int)recipe.Center.Y - Layout.H / 2);

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