using BangDreamLib.Scripts.Nodes.VFX;
using Godot;

namespace BangDreamLib.Scripts.Utils.Infos;

public partial class VfxContext(NBangDreamVfx vfx) : GodotObject
{
    private readonly Dictionary<string, object> _data = new();

    private readonly WeakReference<NBangDreamVfx>? _vfx = new(vfx);

    public NBangDreamVfx? VfxNode => _vfx != null && _vfx.TryGetTarget(out var vfx) ? vfx : null;

    public void Set<T>(string key, T value) => _data[key] = value ?? throw new ArgumentNullException(nameof(value));
    public T? Get<T>(string key) => _data.TryGetValue(key, out var v) ? (T?)v : default;
    public void Remove(string key) => _data.Remove(key);
    public void Clear() => _data.Clear();
}