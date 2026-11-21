using LogicSim.Game.Rendering;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LogicSim.Game.Core;

public sealed class SaveFile
{
    public int Version { get; set; } = 2;
    public string Name { get; set; } = "";
    /// <summary>Путь исходного файла; используется автосохранением для возврата к тому же файлу.</summary>
    public string? SourcePath { get; set; }
    public bool Running { get; set; } = true;
    public Vector2 CameraTarget { get; set; }
    public float CameraZoom { get; set; } = 1f;
    public List<SavedElement> Elements { get; set; } = new();
    public List<SavedWire> Wires { get; set; } = new();
    public List<SavedTemplate> Templates { get; set; } = new();
    public List<SavedTemplate> Library { get; set; } = new();
    public string? RecentWireColorHex { get; set; }
    public List<SavedLayer> Layers { get; set; } = new();
    public int ActiveLayer { get; set; }
}

public sealed class SavedLayer
{
    public string Name { get; set; } = "";
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
}

public sealed class SavedPinState
{
    public int State { get; set; } = (int)LogicState.HighZ;
    /// <summary>32 бита шины; null, если все биты High-Z.</summary>
    public List<int>? Bus { get; set; }
}

public sealed class SavedElement
{
    public string Kind { get; set; } = "";
    public int Id { get; set; }
    public int Layer { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int BitWidth { get; set; } = 1;
    public int PeriodTicks { get; set; } = 60;
    public int DelayTicks { get; set; } = 8;
    public string BodyColor { get; set; } = "";
    public string WireColor { get; set; } = "";
    public List<string>? InputLabels { get; set; }
    public List<string>? OutputLabels { get; set; }
    public List<string>? InputPinColors { get; set; }
    public List<string>? OutputPinColors { get; set; }
    public List<float>? InputPinOffsets { get; set; }
    public List<float>? OutputPinOffsets { get; set; }
    /// <summary>Состояния входов — в том числе переключатели IN-элементов.</summary>
    public List<SavedPinState>? InputPinStates { get; set; }
    public List<SavedPinState>? OutputPinStates { get; set; }
    public SaveFile? ChipContent { get; set; }
}

public sealed class SavedWire
{
    public int Layer { get; set; }
    public int FromId { get; set; }
    public int FromPinIdx { get; set; }
    public int ToId { get; set; }
    public int ToPinIdx { get; set; }
    public List<float> WaypointsXY { get; set; } = new();
    public bool IsBus { get; set; }
}

public sealed class SavedTemplate
{
    public string Name { get; set; } = "";
    public string BodyColor { get; set; } = "";
    public string WireColor { get; set; } = "";
    public Vector2 CameraTarget { get; set; }
    public float CameraZoom { get; set; }
    public SaveFile ChipContent { get; set; } = new();

    public int Width { get; set; }
    public int Height { get; set; }
    public List<string>? InputLabels { get; set; }
    public List<string>? OutputLabels { get; set; }
    public List<string>? InputPinColors { get; set; }
    public List<string>? OutputPinColors { get; set; }
    public List<float>? InputPinOffsets { get; set; }
    public List<float>? OutputPinOffsets { get; set; }
}

public static class SaveSystem
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new Vector2Converter() }
    };

    /// <summary>Vector2 держит публичные поля, а System.Text.Json по умолчанию сериализует только свойства.</summary>
    public sealed class Vector2Converter : JsonConverter<Vector2>
    {
        public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return Vector2.Zero;

            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Vector2.Zero;

            float x = ReadAxis(root, "X");
            float y = ReadAxis(root, "Y");
            return new Vector2(x, y);
        }

        private static float ReadAxis(JsonElement root, string name)
            => root.TryGetProperty(name, out var v) || root.TryGetProperty(name.ToLowerInvariant(), out v)
                ? v.GetSingle()
                : 0f;

        public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("X", value.X);
            writer.WriteNumber("Y", value.Y);
            writer.WriteEndObject();
        }
    }

    public static void Save(string path, Circuit circuit, LayoutAccessor layout,
        Vector2 camTarget, float camZoom,
        List<ChipTemplate> templates, List<ChipTemplate> library,
        string saveName = "", System.Drawing.Color? recentWireColor = null,
        LayerSet? layers = null, string? sourcePath = null)
    {
        var save = SerializeCircuit(circuit, layout);
        save.CameraTarget = camTarget;
        save.CameraZoom = camZoom;
        save.Name = saveName;
        save.SourcePath = sourcePath ?? path;
        save.RecentWireColorHex = recentWireColor.HasValue ? ColorToHex(recentWireColor.Value) : null;
        if (layers is not null) WriteLayers(layers, save);

        foreach (var tpl in templates)
            save.Templates.Add(ToSavedTemplate(tpl));
        foreach (var tpl in library)
            save.Library.Add(ToSavedTemplate(tpl));

        File.WriteAllText(path, JsonSerializer.Serialize(save, Opts));
    }

    public static SaveFile? Load(string path)
    {
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<SaveFile>(File.ReadAllText(path), Opts);
    }

    public static string CaptureSnapshot(Circuit circuit, LayoutAccessor layout, LayerSet? layers = null,
        List<ChipTemplate>? templates = null, List<ChipTemplate>? library = null)
    {
        var save = SerializeCircuit(circuit, layout);
        if (layers is not null) WriteLayers(layers, save);
        if (templates is not null)
            foreach (var tpl in templates) save.Templates.Add(ToSavedTemplate(tpl));
        if (library is not null)
            foreach (var tpl in library) save.Library.Add(ToSavedTemplate(tpl));
        return JsonSerializer.Serialize(save, Opts);
    }

    public static void ApplySnapshot(string snapshot, Circuit circuit, LayoutAccessor layout,
        LayerSet? layers = null,
        List<ChipTemplate>? templates = null, List<ChipTemplate>? library = null)
    {
        var save = JsonSerializer.Deserialize<SaveFile>(snapshot, Opts);
        if (save is null) return;

        circuit.Clear();
        layout.Clear();
        DeserializeInto(save, circuit, layout);
        circuit.Running = save.Running;
        if (layers is not null)
        {
            ReadLayers(save, layers);
            NormalizeLayers(circuit, layers);
        }
        if (templates is not null)
        {
            templates.Clear();
            foreach (var t in save.Templates) templates.Add(FromSavedTemplate(t));
        }
        if (library is not null)
        {
            library.Clear();
            foreach (var t in save.Library) library.Add(FromSavedTemplate(t));
        }
    }

    public static void Apply(SaveFile save, Circuit circuit, LayoutAccessor layout,
        out Vector2 camTarget, out float camZoom,
        List<ChipTemplate> templates, List<ChipTemplate> library,
        LayerSet? layers = null)
    {
        circuit.Clear();
        layout.Clear();
        DeserializeInto(save, circuit, layout);
        circuit.Running = save.Running;
        if (layers is not null)
        {
            ReadLayers(save, layers);
            NormalizeLayers(circuit, layers);
        }

        templates.Clear();
        foreach (var t in save.Templates)
            templates.Add(FromSavedTemplate(t));

        library.Clear();
        foreach (var t in save.Library)
            library.Add(FromSavedTemplate(t));

        camTarget = save.CameraTarget;
        camZoom = save.CameraZoom <= 0 ? 1f : save.CameraZoom;
    }

    private static SavedTemplate ToSavedTemplate(ChipTemplate tpl) => new()
    {
        Name = tpl.Name,
        BodyColor = ColorToHex(tpl.BodyColor),
        WireColor = ColorToHex(tpl.WireColor),
        ChipContent = SerializeChip(tpl.Prototype),

        Width = tpl.Prototype.Width,
        Height = tpl.Prototype.Height,
        InputLabels = tpl.Prototype.InputLabels.ToList(),
        OutputLabels = tpl.Prototype.OutputLabels.ToList(),
        InputPinColors = tpl.Prototype.Inputs
            .Select(p => p.Color.HasValue ? ColorToHex(p.Color.Value) : "")
            .ToList(),
        OutputPinColors = tpl.Prototype.Outputs
            .Select(p => p.Color.HasValue ? ColorToHex(p.Color.Value) : "")
            .ToList(),
        InputPinOffsets = tpl.Prototype.Inputs
            .Select(p => tpl.Prototype.GetOffset(p))
            .ToList(),
        CameraTarget = tpl.CameraTarget,
        CameraZoom = tpl.CameraZoom,
        OutputPinOffsets = tpl.Prototype.Outputs
            .Select(p => tpl.Prototype.GetOffset(p))
            .ToList()
    };

    private static ChipTemplate FromSavedTemplate(SavedTemplate t)
    {
        var chip = DeserializeChip(t.ChipContent, t.Name);

        chip.BodyColor = HexToColor(t.BodyColor);
        chip.WireColor = HexToColor(t.WireColor);

        if (t.Width > 0 && t.Height > 0)
            chip.SetSize(t.Width, t.Height);

        if (t.InputLabels is not null && t.InputLabels.Count == chip.Inputs.Count)
        {
            chip.InputLabels.Clear();
            chip.InputLabels.AddRange(t.InputLabels);
        }

        if (t.OutputLabels is not null && t.OutputLabels.Count == chip.Outputs.Count)
        {
            chip.OutputLabels.Clear();
            chip.OutputLabels.AddRange(t.OutputLabels);
        }

        if (t.InputPinColors is not null)
        {
            for (int i = 0; i < t.InputPinColors.Count && i < chip.Inputs.Count; i++)
                if (!string.IsNullOrEmpty(t.InputPinColors[i]))
                    chip.Inputs[i].Color = HexToColor(t.InputPinColors[i]);
        }

        if (t.OutputPinColors is not null)
        {
            for (int i = 0; i < t.OutputPinColors.Count && i < chip.Outputs.Count; i++)
                if (!string.IsNullOrEmpty(t.OutputPinColors[i]))
                    chip.Outputs[i].Color = HexToColor(t.OutputPinColors[i]);
        }

        if (t.InputPinOffsets is not null)
        {
            for (int i = 0; i < t.InputPinOffsets.Count && i < chip.Inputs.Count; i++)
                chip.SetOffset(chip.Inputs[i], t.InputPinOffsets[i]);
        }

        if (t.OutputPinOffsets is not null)
        {
            for (int i = 0; i < t.OutputPinOffsets.Count && i < chip.Outputs.Count; i++)
                chip.SetOffset(chip.Outputs[i], t.OutputPinOffsets[i]);
        }

        return new ChipTemplate
        {
            Name = t.Name,
        CameraTarget = t.CameraTarget,
        CameraZoom = t.CameraZoom,
            BodyColor = chip.BodyColor,
            WireColor = chip.WireColor,
            Prototype = chip
        };
    }

    private static void WriteLayers(LayerSet layers, SaveFile save)
    {
        save.Layers.Clear();
        foreach (var l in layers.Layers)
            save.Layers.Add(new SavedLayer { Name = l.Name, Visible = l.Visible, Locked = l.Locked });
        save.ActiveLayer = layers.Active;
    }

    private static void ReadLayers(SaveFile save, LayerSet layers)
    {
        var fresh = new LayerSet();
        if (save.Layers.Count > 0)
        {
            // Заполняем через публичный API, чтобы не зависить от внутреннего списка.
            while (fresh.Count < save.Layers.Count)
                if (!fresh.AddLayer()) break;

            for (int i = 0; i < fresh.Count && i < save.Layers.Count; i++)
            {
                var s = save.Layers[i];
                if (!string.IsNullOrEmpty(s.Name)) fresh.Layers[i].Name = s.Name;
                fresh.Layers[i].Visible = s.Visible;
                fresh.Layers[i].Locked = s.Locked;
            }
            fresh.SetActive(save.ActiveLayer);
        }
        layers.CopyFrom(fresh);
    }

    private static void NormalizeLayers(Circuit circuit, LayerSet layers)
    {
        int max = layers.Count - 1;
        foreach (var el in circuit.Elements) el.Layer = Math.Clamp(el.Layer, 0, max);
        foreach (var w in circuit.Wires) w.Layer = Math.Clamp(w.Layer, 0, max);
    }

    private static SaveFile SerializeCircuit(Circuit circuit, LayoutAccessor layout)
    {
        var save = new SaveFile { Running = circuit.Running };
        var idMap = new Dictionary<Element, int>();
        int next = 1;

        foreach (var el in circuit.Elements)
        {
            int id = next++;
            idMap[el] = id;

            var se = new SavedElement
            {
                Kind = KindOf(el),
                Id = id,
                Layer = el.Layer,
                X = layout.GetX(el),
                Y = layout.GetY(el),
                Width = el.Width,
                Height = el.Height,
                BitWidth = el switch
                {
                    BusMerger m => m.BitWidth,
                    BusSplitter s => s.BitWidth,
                    InElement i => i.BitWidth,
                    OutElement o => o.BitWidth,
                    _ => 1
                },
                PeriodTicks = el is ClockElement clock ? clock.PeriodTicks : 60,
                DelayTicks = el is DelayElement delay ? delay.DelayTicks : 8,
                BodyColor = ColorToHex(el.BodyColor),
                WireColor = ColorToHex(el.WireColor),
                InputLabels = el.InputLabels.ToList(),
                OutputLabels = el.OutputLabels.ToList(),
                InputPinColors = el.Inputs.Select(p => p.Color.HasValue ? ColorToHex(p.Color.Value) : "").ToList(),
                OutputPinColors = el.Outputs.Select(p => p.Color.HasValue ? ColorToHex(p.Color.Value) : "").ToList(),
                InputPinStates = CapturePinStates(el.Inputs),
                OutputPinStates = CapturePinStates(el.Outputs),
                ChipContent = el is ChipElement chip ? SerializeChip(chip) : null
            };

            if (el is ChipElement ce)
            {
                se.InputPinOffsets = ce.Inputs.Select(p => ce.GetOffset(p)).ToList();
                se.OutputPinOffsets = ce.Outputs.Select(p => ce.GetOffset(p)).ToList();
            }

            save.Elements.Add(se);
        }

        foreach (var w in circuit.Wires)
        {
            var fo = circuit.OwnerOf(w.From);
            var to = circuit.OwnerOf(w.To);
            if (fo is null || to is null) continue;

            var sw = new SavedWire
            {
                Layer = w.Layer,
                FromId = idMap[fo],
                FromPinIdx = PinIndex(fo, w.From, isInput: false),
                ToId = idMap[to],
                ToPinIdx = PinIndex(to, w.To, isInput: true),
                IsBus = w.IsBus
            };
            foreach (var wp in w.Waypoints) { sw.WaypointsXY.Add(wp.X); sw.WaypointsXY.Add(wp.Y); }
            save.Wires.Add(sw);
        }

        return save;
    }

    private static SaveFile SerializeChip(ChipElement chip)
    {
        var save = new SaveFile { Name = chip.Name };
        var idMap = new Dictionary<Element, int>();
        int next = 1;

        foreach (var el in chip.Inner)
        {
            int id = next++;
            idMap[el] = id;
            chip.InnerPositions.TryGetValue(el, out var pos);

            var se = new SavedElement
            {
                Kind = KindOf(el),
                Id = id,
                Layer = el.Layer,
                X = pos.X,
                Y = pos.Y,
                Width = el.Width,
                Height = el.Height,
                BitWidth = el switch
                {
                    BusMerger m => m.BitWidth,
                    BusSplitter s => s.BitWidth,
                    InElement i => i.BitWidth,
                    OutElement o => o.BitWidth,
                    _ => 1
                },
                PeriodTicks = el is ClockElement clock ? clock.PeriodTicks : 60,
                DelayTicks = el is DelayElement delay ? delay.DelayTicks : 8,
                BodyColor = ColorToHex(el.BodyColor),
                WireColor = ColorToHex(el.WireColor),
                InputLabels = el.InputLabels.ToList(),
                OutputLabels = el.OutputLabels.ToList(),
                InputPinColors = el.Inputs.Select(p => p.Color.HasValue ? ColorToHex(p.Color.Value) : "").ToList(),
                OutputPinColors = el.Outputs.Select(p => p.Color.HasValue ? ColorToHex(p.Color.Value) : "").ToList(),
                InputPinStates = CapturePinStates(el.Inputs),
                OutputPinStates = CapturePinStates(el.Outputs),
                ChipContent = el is ChipElement c2 ? SerializeChip(c2) : null
            };

            if (el is ChipElement ce)
            {
                se.InputPinOffsets = ce.Inputs.Select(p => ce.GetOffset(p)).ToList();
                se.OutputPinOffsets = ce.Outputs.Select(p => ce.GetOffset(p)).ToList();
            }

            save.Elements.Add(se);
        }

        foreach (var w in chip.InnerWires)
        {
            var fo = FindOwnerIn(chip.Inner, w.From);
            var to = FindOwnerIn(chip.Inner, w.To);
            if (fo is null || to is null) continue;

            var sw = new SavedWire
            {
                Layer = w.Layer,
                FromId = idMap[fo],
                FromPinIdx = PinIndex(fo, w.From, isInput: false),
                ToId = idMap[to],
                ToPinIdx = PinIndex(to, w.To, isInput: true),
                IsBus = w.IsBus
            };
            foreach (var wp in w.Waypoints) { sw.WaypointsXY.Add(wp.X); sw.WaypointsXY.Add(wp.Y); }
            save.Wires.Add(sw);
        }

        WriteLayers(chip.LayerState, save);
        return save;
    }

    private static void DeserializeInto(SaveFile save, Circuit circuit, LayoutAccessor layout)
    {
        var idMap = new Dictionary<int, Element>();

        foreach (var s in save.Elements)
        {
            Element el = s.Kind switch
            {
                "IN" => new InElement(s.BitWidth),
                "OUT" => new OutElement(s.BitWidth),
                "NAND" => new NandElement(),
                "3STATE" => new TriStateBufferElement(),
                "PULLUP" => new PullUpElement(),
                "PULLDOWN" => new PullDownElement(),
                "CLK" => new ClockElement(),
                "DELAY" => new DelayElement(),
                "MERGE" => new BusMerger(s.BitWidth),
                "SPLIT" => new BusSplitter(s.BitWidth),
                "CHIP" => s.ChipContent is not null
                    ? DeserializeChip(s.ChipContent, s.ChipContent.Name)
                    : new ChipElement("EMPTY", Array.Empty<Element>(), Array.Empty<Wire>(),
                        Array.Empty<(InputPin, InputPin)>(), Array.Empty<(InputPin, OutputPin)>()),
                _ => new NandElement()
            };

            ApplyElementCommon(el, s);
            circuit.AddElement(el);
            layout.Place(el, s.X, s.Y);
            idMap[s.Id] = el;
        }

        foreach (var sw in save.Wires)
        {
            if (!idMap.TryGetValue(sw.FromId, out var fromEl)) continue;
            if (!idMap.TryGetValue(sw.ToId, out var toEl)) continue;

            var fromPin = PinByIndex(fromEl, sw.FromPinIdx, isInput: false);
            var toPin = PinByIndex(toEl, sw.ToPinIdx, isInput: true);
            if (fromPin is OutputPin op && toPin is InputPin ip && op.BitWidth == ip.BitWidth)
            {
                if (circuit.ContainsWire(op, ip)) continue;
                var w = circuit.Connect(op, ip);
                w.IsBus = sw.IsBus;
                w.Layer = sw.Layer;
                for (int i = 0; i + 1 < sw.WaypointsXY.Count; i += 2)
                    w.Waypoints.Add(new Vector2(sw.WaypointsXY[i], sw.WaypointsXY[i + 1]));
            }
        }
    }

    private static List<SavedPinState>? CapturePinStates(IReadOnlyList<Pin> pins)
    {
        if (pins.Count == 0) return null;

        var result = new List<SavedPinState>(pins.Count);
        foreach (var pin in pins)
        {
            var bits = pin.BusStates;
            List<int>? bus = null;
            for (int i = 0; i < bits.Length; i++)
            {
                if (bits[i] == LogicState.HighZ) continue;
                bus = bits.Select(b => (int)b).ToList();
                break;
            }

            result.Add(new SavedPinState { State = (int)pin.State, Bus = bus });
        }
        return result;
    }

    private static void ApplyPinStates(IReadOnlyList<Pin> pins, List<SavedPinState>? states)
    {
        if (states is null) return;

        for (int i = 0; i < states.Count && i < pins.Count; i++)
        {
            var s = states[i];
            if (s.State >= (int)LogicState.Zero && s.State <= (int)LogicState.Conflict)
                pins[i].SetValue((LogicState)s.State);

            if (s.Bus is null) continue;
            var bits = Enumerable.Repeat(LogicState.HighZ, 32).ToArray();
            for (int b = 0; b < bits.Length && b < s.Bus.Count; b++)
                if (s.Bus[b] >= (int)LogicState.Zero && s.Bus[b] <= (int)LogicState.Conflict)
                    bits[b] = (LogicState)s.Bus[b];
            pins[i].BusStates = bits;
        }
    }

    private static void ApplyElementCommon(Element el, SavedElement s)
    {
        el.Layer = s.Layer;
        el.BodyColor = HexToColor(s.BodyColor);
        el.WireColor = HexToColor(s.WireColor);

        if (el is ClockElement clock) clock.SetPeriod(s.PeriodTicks);
        if (el is DelayElement delay) delay.SetDelay(s.DelayTicks);

        if (s.InputLabels is not null && s.InputLabels.Count == el.Inputs.Count)
        {
            el.InputLabels.Clear(); el.InputLabels.AddRange(s.InputLabels);
        }
        if (s.OutputLabels is not null && s.OutputLabels.Count == el.Outputs.Count)
        {
            el.OutputLabels.Clear(); el.OutputLabels.AddRange(s.OutputLabels);
        }

        if (s.InputPinColors is not null)
            for (int i = 0; i < s.InputPinColors.Count && i < el.Inputs.Count; i++)
                if (!string.IsNullOrEmpty(s.InputPinColors[i]))
                    el.Inputs[i].Color = HexToColor(s.InputPinColors[i]);
        if (s.OutputPinColors is not null)
            for (int i = 0; i < s.OutputPinColors.Count && i < el.Outputs.Count; i++)
                if (!string.IsNullOrEmpty(s.OutputPinColors[i]))
                    el.Outputs[i].Color = HexToColor(s.OutputPinColors[i]);

        if (el is ChipElement chip)
        {
            if (s.Width > 0 && s.Height > 0)
                chip.SetSize(s.Width, s.Height);
            if (s.InputPinOffsets is not null)
                for (int i = 0; i < s.InputPinOffsets.Count && i < chip.Inputs.Count; i++)
                    chip.SetOffset(chip.Inputs[i], s.InputPinOffsets[i]);
            if (s.OutputPinOffsets is not null)
                for (int i = 0; i < s.OutputPinOffsets.Count && i < chip.Outputs.Count; i++)
                    chip.SetOffset(chip.Outputs[i], s.OutputPinOffsets[i]);
        }

        ApplyPinStates(el.Inputs, s.InputPinStates);
        ApplyPinStates(el.Outputs, s.OutputPinStates);
    }

    private static ChipElement DeserializeChip(SaveFile content, string fallbackName)
    {
        var name = !string.IsNullOrEmpty(content.Name) ? content.Name : fallbackName;
        if (string.IsNullOrEmpty(name)) name = "CHIP";

        var inner = new List<Element>();
        var idMap = new Dictionary<int, Element>();

        foreach (var s in content.Elements)
        {
            Element el = s.Kind switch
            {
                "IN" => new InElement(s.BitWidth),
                "OUT" => new OutElement(s.BitWidth),
                "NAND" => new NandElement(),
                "3STATE" => new TriStateBufferElement(),
                "PULLUP" => new PullUpElement(),
                "PULLDOWN" => new PullDownElement(),
                "CLK" => new ClockElement(),
                "DELAY" => new DelayElement(),
                "MERGE" => new BusMerger(s.BitWidth),
                "SPLIT" => new BusSplitter(s.BitWidth),
                "CHIP" => s.ChipContent is not null
                    ? DeserializeChip(s.ChipContent, s.ChipContent.Name)
                    : new ChipElement("EMPTY", Array.Empty<Element>(), Array.Empty<Wire>(),
                        Array.Empty<(InputPin, InputPin)>(), Array.Empty<(InputPin, OutputPin)>()),
                _ => new NandElement()
            };

            ApplyElementCommon(el, s);
            inner.Add(el);
            idMap[s.Id] = el;
        }

        var wires = new List<Wire>();
        foreach (var sw in content.Wires)
        {
            if (!idMap.TryGetValue(sw.FromId, out var fromEl)) continue;
            if (!idMap.TryGetValue(sw.ToId, out var toEl)) continue;

            var fromPin = PinByIndex(fromEl, sw.FromPinIdx, isInput: false);
            var toPin = PinByIndex(toEl, sw.ToPinIdx, isInput: true);
            if (fromPin is OutputPin op && toPin is InputPin ip && op.BitWidth == ip.BitWidth)
            {
                var w = new Wire(op, ip);
                w.IsBus = sw.IsBus;
                w.Layer = sw.Layer;
                for (int i = 0; i + 1 < sw.WaypointsXY.Count; i += 2)
                    w.Waypoints.Add(new Vector2(sw.WaypointsXY[i], sw.WaypointsXY[i + 1]));
                wires.Add(w);
            }
        }

        var inputs = new List<(InputPin ext, InputPin inner)>();
        var outputs = new List<(InputPin inner, OutputPin ext)>();
        foreach (var el in inner)
        {
            if (el is InElement ie)
            {
                var ext = new InputPin { BitWidth = ie.Out.BitWidth };
                inputs.Add((ext, ie.In[0]));
            }
            if (el is OutElement oe)
            {
                var ext = new OutputPin { BitWidth = oe.In.BitWidth };
                outputs.Add((oe.In, ext));
            }
        }

        var chip = new ChipElement(name, inner.ToArray(), wires.ToArray(),
            inputs.ToArray(), outputs.ToArray());
        ReadLayers(content, chip.LayerState);
        int maxLayer = chip.LayerState.Count - 1;
        foreach (var el in inner) el.Layer = Math.Clamp(el.Layer, 0, maxLayer);
        foreach (var w in wires) w.Layer = Math.Clamp(w.Layer, 0, maxLayer);

        foreach (var s in content.Elements)
            if (idMap.TryGetValue(s.Id, out var el))
                chip.SetInnerPosition(el, s.X, s.Y);

        return chip;
    }

    private static string KindOf(Element el) => el switch
    {
        InElement => "IN",
        OutElement => "OUT",
        NandElement => "NAND",
        TriStateBufferElement => "3STATE",
        PullUpElement => "PULLUP",
        PullDownElement => "PULLDOWN",
        ClockElement => "CLK",
        DelayElement => "DELAY",
        BusMerger => "MERGE",
        BusSplitter => "SPLIT",
        ChipElement => "CHIP",
        _ => "NAND"
    };

    private static int PinIndex(Element el, Pin pin, bool isInput)
    {
        if (isInput)
        {
            for (int i = 0; i < el.Inputs.Count; i++)
                if (ReferenceEquals(el.Inputs[i], pin)) return i;
        }
        else
        {
            for (int i = 0; i < el.Outputs.Count; i++)
                if (ReferenceEquals(el.Outputs[i], pin)) return i;
        }
        return 0;
    }

    private static Pin? PinByIndex(Element el, int index, bool isInput)
    {
        if (isInput)
        {
            if (index >= 0 && index < el.Inputs.Count) return el.Inputs[index];
        }
        else
        {
            if (index >= 0 && index < el.Outputs.Count) return el.Outputs[index];
        }
        return null;
    }

    private static Element? FindOwnerIn(IReadOnlyList<Element> set, Pin pin)
    {
        foreach (var el in set)
        {
            foreach (var p in el.Inputs) if (ReferenceEquals(p, pin)) return el;
            foreach (var p in el.Outputs) if (ReferenceEquals(p, pin)) return el;
        }
        return null;
    }

    public static string ColorToHex(System.Drawing.Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static System.Drawing.Color HexToColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || hex.Length < 7)
            return System.Drawing.Color.FromArgb(45, 50, 70);
        try
        {
            return System.Drawing.Color.FromArgb(
                Convert.ToByte(hex.Substring(1, 2), 16),
                Convert.ToByte(hex.Substring(3, 2), 16),
                Convert.ToByte(hex.Substring(5, 2), 16));
        }
        catch { return System.Drawing.Color.FromArgb(45, 50, 70); }
    }

    public static System.Drawing.Color? FromHexOrNull(string? hex)
        => string.IsNullOrWhiteSpace(hex) ? null : HexToColor(hex);
}

public interface LayoutAccessor
{
    int GetX(Element el);
    int GetY(Element el);
    void Place(Element el, int x, int y);
    void Clear();
}