namespace LogicSim.Game.Core;

public sealed class LayerInfo
{
    public string Name { get; set; } = "";
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
}

/// <summary>Набор слоёв одного контекста редактирования (корень или микросхема).</summary>
public sealed class LayerSet
{
    private readonly List<LayerInfo> _layers = new();

    public LayerSet()
    {
        _layers.Add(new LayerInfo { Name = "L1" });
    }

    public IReadOnlyList<LayerInfo> Layers => _layers;
    public int Count => _layers.Count;
    public int Active { get; private set; }

    public bool IsVisible(int layer)
        => layer < 0 || layer >= _layers.Count || _layers[layer].Visible;

    public bool IsLocked(int layer)
        => layer >= 0 && layer < _layers.Count && _layers[layer].Locked;

    public bool CanEdit(int layer) => IsVisible(layer) && !IsLocked(layer);

    public void SetActive(int index)
    {
        if (_layers.Count == 0) return;
        Active = Math.Clamp(index, 0, _layers.Count - 1);
    }

    public bool AddLayer()
    {
        int n = _layers.Count + 1;
        while (_layers.Exists(l => l.Name == $"L{n}")) n++;
        _layers.Add(new LayerInfo { Name = $"L{n}" });
        Active = _layers.Count - 1;
        return true;
    }

    /// <summary>Удаляет слой; последний слой удалить нельзя.</summary>
    public bool RemoveLayer(int index)
    {
        if (_layers.Count <= 1 || index < 0 || index >= _layers.Count) return false;

        _layers.RemoveAt(index);
        Active = Math.Clamp(index < Active ? Active - 1 : Active, 0, _layers.Count - 1);
        if (!_layers[Active].Visible)
        {
            int fallback = _layers.FindIndex(l => l.Visible);
            if (fallback >= 0) Active = fallback;
        }
        return true;
    }

    public void ToggleVisible(int index)
    {
        if (index < 0 || index >= _layers.Count) return;
        bool newValue = !_layers[index].Visible;
        _layers[index].Visible = newValue;

        if (!newValue && Active == index)
        {
            int fallback = -1;
            for (int i = 0; i < _layers.Count; i++)
                if (_layers[i].Visible) { fallback = i; break; }
            if (fallback >= 0) Active = fallback;
        }
    }

    public void ToggleLocked(int index)
    {
        if (index < 0 || index >= _layers.Count) return;
        _layers[index].Locked = !_layers[index].Locked;
    }

    public void CopyFrom(LayerSet other)
    {
        if (ReferenceEquals(this, other)) return;

        _layers.Clear();
        for (int i = 0; i < other._layers.Count; i++)
            _layers.Add(new LayerInfo
            {
                Name = other._layers[i].Name,
                Visible = other._layers[i].Visible,
                Locked = other._layers[i].Locked
            });
        if (_layers.Count == 0) _layers.Add(new LayerInfo { Name = "L1" });
        Active = Math.Clamp(other.Active, 0, _layers.Count - 1);
    }
}
