using BangDreamLib.Scripts.Nodes.VFX;
using Godot;
using MegaCrit.Sts2.Core.Assets;

namespace BangDreamLib.Scripts.Utils.Builder;

public class VfxCreator<T>(string scene)
    where T : NBangDreamVfx
{
    private PackedScene Scene { get; } = PreloadManager.Cache.GetScene(scene);

    private List<Func<T, VfxContext, Task>> _spawnEventList = [];
    private List<Func<T, VfxContext, Task>> _beforeHitEventList = [];
    private List<Func<T, VfxContext, Task>> _hitEventList = [];
    private List<Func<T, VfxContext, Task>> _afterHitEventList = [];
    private List<Func<T, VfxContext, Task>> _finishEventList = [];

    public VfxCreator<T> SetOnSpawn(Func<T, VfxContext, Task> onSpawn)
    {
        _spawnEventList = [onSpawn];
        return this;
    }

    public VfxCreator<T> AddOnSpawn(Func<T, VfxContext, Task> onSpawn)
    {
        _spawnEventList.Add(onSpawn);
        return this;
    }

    public VfxCreator<T> SetOnBeforeHit(Func<T, VfxContext, Task> onBeforeHit)
    {
        _beforeHitEventList = [onBeforeHit];
        return this;
    }

    public VfxCreator<T> AddOnBeforeHit(Func<T, VfxContext, Task> onBeforeHit)
    {
        _beforeHitEventList.Add(onBeforeHit);
        return this;
    }

    public VfxCreator<T> SetOnHit(Func<T, VfxContext, Task> onHit)
    {
        _hitEventList = [onHit];
        return this;
    }

    public VfxCreator<T> AddOnHit(Func<T, VfxContext, Task> onHit)
    {
        _hitEventList.Add(onHit);
        return this;
    }

    public VfxCreator<T> SetOnAfterHit(Func<T, VfxContext, Task> onAfterHit)
    {
        _afterHitEventList = [onAfterHit];
        return this;
    }

    public VfxCreator<T> AddOnAfterHit(Func<T, VfxContext, Task> onAfterHit)
    {
        _afterHitEventList.Add(onAfterHit);
        return this;
    }

    public VfxCreator<T> SetOnFinish(Func<T, VfxContext, Task> onFinish)
    {
        _finishEventList = [onFinish];
        return this;
    }

    public VfxCreator<T> AddOnFinish(Func<T, VfxContext, Task> onFinish)
    {
        _finishEventList.Add(onFinish);
        return this;
    }

    private void WireEvents(T vfx, VfxContext vfxContext)
    {
        foreach (var func in _spawnEventList)
        {
            vfx.VfxSpawned += async () => await func(vfx, vfxContext);
        }

        foreach (var func in _beforeHitEventList)
        {
            vfx.BeforeHit += async () => await func(vfx, vfxContext);
        }

        foreach (var func in _hitEventList)
        {
            vfx.HitTriggered += async () => await func(vfx, vfxContext);
        }

        foreach (var func in _afterHitEventList)
        {
            vfx.AfterHit += async () => await func(vfx, vfxContext);
        }

        foreach (var func in _finishEventList)
        {
            vfx.VfxFinished += async () => await func(vfx, vfxContext);
        }
    }

    public T Create()
    {
        var vfx = Scene.Instantiate<T>();
        var vfxContext = new VfxContext();
        vfxContext.Set("index", 0);
        WireEvents(vfx, vfxContext);
        return vfx;
    }

    public IEnumerable<T> CreateBatch(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var vfx = Scene.Instantiate<T>();
            var vfxContext = new VfxContext();
            vfxContext.Set("index", i);
            WireEvents(vfx, vfxContext);
            yield return vfx;
        }
    }
}

public class VfxContext
{
    private readonly Dictionary<string, object> _data = new();

    public void Set<T>(string key, T value) => _data[key] = value ?? throw new ArgumentNullException(nameof(value));
    public T? Get<T>(string key) => _data.TryGetValue(key, out var v) ? (T?)v : default;
    public void Remove(string key) => _data.Remove(key);
    public void Clear() => _data.Clear();

    internal VfxContext()
    {
    }
}