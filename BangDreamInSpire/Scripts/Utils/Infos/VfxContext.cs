using BangDreamInSpire.Scripts.Enums;
using Godot;

namespace BangDreamInSpire.Scripts.Utils.Infos;

public partial class VfxContext : GodotObject
{
    private readonly Dictionary<string, object> _data = new();

    public VfxLifecycle Lifecycle { get; set; } = VfxLifecycle.Spawn;

    public void Set<T>(string key, T value) => _data[key] = value ?? throw new ArgumentNullException(nameof(value));
    public T? Get<T>(string key) => _data.TryGetValue(key, out var v) && v is T t ? t : default;
    public void Remove(string key) => _data.Remove(key);
    public void Clear() => _data.Clear();
}
