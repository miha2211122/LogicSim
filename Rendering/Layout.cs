using LogicSim.Game.Core;
using System.Numerics;

namespace LogicSim.Game.Rendering;

public sealed class Layout : LayoutAccessor
{
    public const int W = Element.DefaultWidth;
    public const int H = Element.DefaultHeight;

    public const float VisualPinRadius = 10f;
    public const float HitPinRadius = 14f;
    public const float InInputHitRadius = 32f;

    private readonly Dictionary<Element, (int X, int Y)> _pos = new();

    public void Place(Element el, int x, int y) => _pos[el] = (x, y);
    public void Remove(Element el) => _pos.Remove(el);
    public void Clear() => _pos.Clear();
    public bool TryGet(Element el, out (int X, int Y) p) => _pos.TryGetValue(el, out p);

    public int GetX(Element el) => _pos.TryGetValue(el, out var p) ? p.X : 0;
    public int GetY(Element el) => _pos.TryGetValue(el, out var p) ? p.Y : 0;

    public int PinScreenY(Element el, Pin pin, bool isInput)
    {
        if (!_pos.TryGetValue(el, out var p)) return 0;

        if (el is ChipElement chip)
            return p.Y + (int)(chip.GetOffset(pin) * chip.Height);

        // У OutElement пин Out лежит вне Outputs (терминал) — обрабатываем отдельно,
        // иначе PinY получит count=0 и вернёт координату низа элемента.
        if (el is OutElement oe && ReferenceEquals(oe.Out, pin))
            return p.Y + el.Height / 2;

        int count = isInput ? el.Inputs.Count : el.Outputs.Count;
        int idx = PinIndex(el, pin, isInput);
        return PinY(p.Y, el.Height, count, idx);
    }

    public static int PinY(int top, int count, int index)
        => count == 1 ? top + H / 2 : top + H / (count + 1) * (index + 1);

    public static int PinY(int top, int height, int count, int index)
        => count == 1 ? top + height / 2 : top + height / (count + 1) * (index + 1);

    public Element? ElementAt(Vector2 m, IReadOnlyList<Element> elements)
    {
        foreach (var el in elements)
        {
            if (!_pos.TryGetValue(el, out var p)) continue;
            if (m.X >= p.X && m.X <= p.X + el.Width && m.Y >= p.Y && m.Y <= p.Y + el.Height)
                return el;
        }
        return null;
    }

    public PinHit? PinAt(Vector2 m, IReadOnlyList<Element> elements)
    {
        foreach (var el in elements)
        {
            if (!_pos.TryGetValue(el, out var p)) continue;

            for (int i = 0; i < el.Inputs.Count; i++)
            {
                var pin = el.Inputs[i];
                int py = PinScreenY(el, pin, isInput: true);
                var pt = new Vector2(p.X, py);
                float r = el is InElement ? InInputHitRadius : HitPinRadius;
                if (Vector2.Distance(m, pt) <= r)
                    return new PinHit(el, pin, true);
            }
            for (int i = 0; i < el.Outputs.Count; i++)
            {
                var pin = el.Outputs[i];
                int py = PinScreenY(el, pin, isInput: false);
                var pt = new Vector2(p.X + el.Width, py);
                if (Vector2.Distance(m, pt) <= HitPinRadius)
                    return new PinHit(el, pin, false);
            }
        }
        return null;
    }

    public Vector2? PinPosition(Element el, Pin pin)
    {
        if (!_pos.TryGetValue(el, out var p)) return null;

        for (int i = 0; i < el.Inputs.Count; i++)
            if (ReferenceEquals(el.Inputs[i], pin))
                return new Vector2(p.X, PinScreenY(el, pin, isInput: true));

        for (int i = 0; i < el.Outputs.Count; i++)
            if (ReferenceEquals(el.Outputs[i], pin))
                return new Vector2(p.X + el.Width, PinScreenY(el, pin, isInput: false));

        if (el is OutElement oe && ReferenceEquals(oe.Out, pin))
            return new Vector2(p.X + el.Width, PinScreenY(el, pin, isInput: false));

        return null;
    }

    public static int PinIndex(Element el, Pin pin, bool isInput)
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
        return -1;
    }
}

public readonly record struct PinHit(Element Element, Pin Pin, bool IsInput);