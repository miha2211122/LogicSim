using LogicSim.Game.Rendering;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LogicSim.Game.Core;

public sealed class SaveFile
{
    public int Version { get; set; } = 2;
    public string Name { get; set; } = "";
    public Vector2 CameraTarget { get; set; }
    public float CameraZoom { get; set; } = 1f;
    public List<SavedElement> Elements { get; set; } = new();
    public List<SavedWire> Wires { get; set; } = new();
    public List<SavedTemplate> Templates { get; set; } = new();
}

public sealed class SavedElement
{
    public string Kind { get; set; } = "";
    public int Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string BodyColor { get; set; } = "";
    public string WireColor { get; set; } = "";
    public List<string>? InputLabels { get; set; }
    public List<string>? OutputLabels { get; set; }
    public SaveFile? ChipContent { get; set; }
}

public sealed class SavedWire
{
    public int FromId { get; set; }
    public int FromPinIdx { get; set; }
    public int ToId { get; set; }
    public int ToPinIdx { get; set; }
    public List<float> WaypointsXY { get; set; } = new();
}

public sealed class SavedTemplate
{
    public string Name { get; set; } = "";
    public string BodyColor { get; set; } = "";
    public string WireColor { get; set; } = "";
    public SaveFile ChipContent { get; set; } = new();
}

public static class SaveSystem
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void Save(string path, Circuit circuit, LayoutAccessor layout,
        Vector2 camTarget, float camZoom, List<ChipTemplate> templates, string saveName = "")
    {
        var save = SerializeCircuit(circuit, layout);
        save.CameraTarget = camTarget;
        save.CameraZoom = camZoom;
        save.Name = saveName;

        foreach (var tpl in templates)
        {
            save.Templates.Add(new SavedTemplate
            {
                Name = tpl.Name,
                BodyColor = ColorToHex(tpl.BodyColor),
                WireColor = ColorToHex(tpl.WireColor),
                ChipContent = SerializeChip(tpl.Prototype)
            });
        }

        File.WriteAllText(path, JsonSerializer.Serialize(save, Opts));
    }

    public static SaveFile? Load(string path)
    {
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<SaveFile>(File.ReadAllText(path), Opts);
    }

    public static void Apply(SaveFile save, Circuit circuit, LayoutAccessor layout,
        out Vector2 camTarget, out float camZoom, List<ChipTemplate> templates)
    {
        circuit.Clear();
        layout.Clear();
        DeserializeInto(save, circuit, layout);

        templates.Clear();
        foreach (var t in save.Templates)
        {
            var chip = DeserializeChip(t.ChipContent, t.Name);
            chip.BodyColor = HexToColor(t.BodyColor);
            chip.WireColor = HexToColor(t.WireColor);
            templates.Add(new ChipTemplate
            {
                Name = t.Name,
                BodyColor = chip.BodyColor,
                WireColor = chip.WireColor,
                Prototype = chip
            });
        }

        camTarget = save.CameraTarget;
        camZoom = save.CameraZoom <= 0 ? 1f : save.CameraZoom;
    }

    private static SaveFile SerializeCircuit(Circuit circuit, LayoutAccessor layout)
    {
        var save = new SaveFile();
        var idMap = new Dictionary<Element, int>();
        int next = 1;

        foreach (var el in circuit.Elements)
        {
            int id = next++;
            idMap[el] = id;
            save.Elements.Add(new SavedElement
            {
                Kind = KindOf(el),
                Id = id,
                X = layout.GetX(el),
                Y = layout.GetY(el),
                BodyColor = ColorToHex(el.BodyColor),
                WireColor = ColorToHex(el.WireColor),
                InputLabels = el.InputLabels.ToList(),
                OutputLabels = el.OutputLabels.ToList(),
                ChipContent = el is ChipElement chip ? SerializeChip(chip) : null
            });
        }

        foreach (var w in circuit.Wires)
        {
            var fo = circuit.OwnerOf(w.From);
            var to = circuit.OwnerOf(w.To);
            if (fo is null || to is null) continue;

            var sw = new SavedWire
            {
                FromId = idMap[fo],
                FromPinIdx = PinIndex(fo, w.From, isInput: false),
                ToId = idMap[to],
                ToPinIdx = PinIndex(to, w.To, isInput: true)
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

            save.Elements.Add(new SavedElement
            {
                Kind = KindOf(el),
                Id = id,
                X = pos.X,
                Y = pos.Y,
                BodyColor = ColorToHex(el.BodyColor),
                WireColor = ColorToHex(el.WireColor),
                InputLabels = el.InputLabels.ToList(),
                OutputLabels = el.OutputLabels.ToList(),
                ChipContent = el is ChipElement c2 ? SerializeChip(c2) : null
            });
        }

        foreach (var w in chip.InnerWires)
        {
            var fo = FindOwnerIn(chip.Inner, w.From);
            var to = FindOwnerIn(chip.Inner, w.To);
            if (fo is null || to is null) continue;

            var sw = new SavedWire
            {
                FromId = idMap[fo],
                FromPinIdx = PinIndex(fo, w.From, isInput: false),
                ToId = idMap[to],
                ToPinIdx = PinIndex(to, w.To, isInput: true)
            };
            foreach (var wp in w.Waypoints) { sw.WaypointsXY.Add(wp.X); sw.WaypointsXY.Add(wp.Y); }
            save.Wires.Add(sw);
        }

        return save;
    }

    private static void DeserializeInto(SaveFile save, Circuit circuit, LayoutAccessor layout)
    {
        var idMap = new Dictionary<int, Element>();

        foreach (var s in save.Elements)
        {
            Element el = s.Kind switch
            {
                "IN" => new InElement(),
                "OUT" => new OutElement(),
                "NAND" => new NandElement(),
                "CHIP" => s.ChipContent is not null
                    ? DeserializeChip(s.ChipContent, s.ChipContent.Name)
                    : new ChipElement("EMPTY", Array.Empty<Element>(), Array.Empty<Wire>(),
                        Array.Empty<(InputPin, InputPin)>(), Array.Empty<(OutputPin, OutputPin)>()),
                _ => new NandElement()
            };

            el.BodyColor = HexToColor(s.BodyColor);
            el.WireColor = HexToColor(s.WireColor);

            if (s.InputLabels is not null && s.InputLabels.Count == el.Inputs.Count)
            {
                el.InputLabels.Clear();
                el.InputLabels.AddRange(s.InputLabels);
            }
            if (s.OutputLabels is not null && s.OutputLabels.Count == el.Outputs.Count)
            {
                el.OutputLabels.Clear();
                el.OutputLabels.AddRange(s.OutputLabels);
            }

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
            if (fromPin is OutputPin op && toPin is InputPin ip)
            {
                var w = circuit.Connect(op, ip);
                for (int i = 0; i + 1 < sw.WaypointsXY.Count; i += 2)
                    w.Waypoints.Add(new Vector2(sw.WaypointsXY[i], sw.WaypointsXY[i + 1]));
            }
        }
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
                "IN" => new InElement(),
                "OUT" => new OutElement(),
                "NAND" => new NandElement(),
                "CHIP" => s.ChipContent is not null
                    ? DeserializeChip(s.ChipContent, s.ChipContent.Name)
                    : new ChipElement("EMPTY", Array.Empty<Element>(), Array.Empty<Wire>(),
                        Array.Empty<(InputPin, InputPin)>(), Array.Empty<(OutputPin, OutputPin)>()),
                _ => new NandElement()
            };

            el.BodyColor = HexToColor(s.BodyColor);
            el.WireColor = HexToColor(s.WireColor);

            if (s.InputLabels is not null && s.InputLabels.Count == el.Inputs.Count)
            {
                el.InputLabels.Clear();
                el.InputLabels.AddRange(s.InputLabels);
            }
            if (s.OutputLabels is not null && s.OutputLabels.Count == el.Outputs.Count)
            {
                el.OutputLabels.Clear();
                el.OutputLabels.AddRange(s.OutputLabels);
            }

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
            if (fromPin is OutputPin op && toPin is InputPin ip)
            {
                var w = new Wire(op, ip);
                for (int i = 0; i + 1 < sw.WaypointsXY.Count; i += 2)
                    w.Waypoints.Add(new Vector2(sw.WaypointsXY[i], sw.WaypointsXY[i + 1]));
                wires.Add(w);
            }
        }

        var inputs = new List<(InputPin ext, InputPin inner)>();
        var outputs = new List<(OutputPin inner, OutputPin ext)>();
        foreach (var el in inner)
        {
            if (el is InElement ie) inputs.Add((new InputPin(), ie.In));
            if (el is OutElement oe) outputs.Add((oe.Out, new OutputPin()));
        }

        var chip = new ChipElement(name, inner.ToArray(), wires.ToArray(),
            inputs.ToArray(), outputs.ToArray());

        foreach (var s in content.Elements)
        {
            if (idMap.TryGetValue(s.Id, out var el))
                chip.SetInnerPosition(el, s.X, s.Y);
        }

        return chip;
    }

    private static string KindOf(Element el) => el switch
    {
        InElement => "IN",
        OutElement => "OUT",
        NandElement => "NAND",
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
            if (el is OutElement oe && ReferenceEquals(oe.Out, pin)) return 0;
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
            if (el is OutElement oe && index == 0) return oe.Out;
        }
        return null;
    }

    private static Element? FindOwnerIn(IReadOnlyList<Element> set, Pin pin)
    {
        foreach (var el in set)
        {
            foreach (var p in el.Inputs) if (ReferenceEquals(p, pin)) return el;
            foreach (var p in el.Outputs) if (ReferenceEquals(p, pin)) return el;
            if (el is OutElement oe && ReferenceEquals(oe.Out, pin)) return el;
        }
        return null;
    }

    private static string ColorToHex(System.Drawing.Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static System.Drawing.Color HexToColor(string hex)
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
}

public interface LayoutAccessor
{
    int GetX(Element el);
    int GetY(Element el);
    void Place(Element el, int x, int y);
    void Clear();
}