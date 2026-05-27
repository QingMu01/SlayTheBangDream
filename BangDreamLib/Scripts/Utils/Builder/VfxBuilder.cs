using BangDreamLib.Scripts.Nodes.VFX;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace BangDreamLib.Scripts.Utils.Builder;

public class VfxBuilder<T> where T : NBangDreamVfx
{
    private PackedScene Scene { get; }

    private int _repeatCount = 1;

    private List<Func<T, VfxContext, Task>> _spawnEventList = [];
    private List<Func<T, VfxContext, Task>> _beforeHitEventList = [];
    private List<Func<T, VfxContext, Task>> _hitEventList = [];
    private List<Func<T, VfxContext, Task>> _afterHitEventList = [];
    private List<Func<T, VfxContext, Task>> _finishEventList = [];

    public VfxBuilder(string scene)
    {
        Scene = PreloadManager.Cache.GetScene(scene);
    }

    public VfxBuilder(PackedScene scene)
    {
        Scene = scene;
    }

    public VfxBuilder<T> RepeatCount(int count)
    {
        _repeatCount = count;
        return this;
    }

    public VfxBuilder<T> SetOnSpawn(Func<T, VfxContext, Task> onSpawn)
    {
        _spawnEventList = [onSpawn];
        return this;
    }

    public VfxBuilder<T> AddOnSpawn(Func<T, VfxContext, Task> onSpawn)
    {
        _spawnEventList.Add(onSpawn);
        return this;
    }

    public VfxBuilder<T> SetOnBeforeHit(Func<T, VfxContext, Task> onBeforeHit)
    {
        _beforeHitEventList = [onBeforeHit];
        return this;
    }

    public VfxBuilder<T> AddOnBeforeHit(Func<T, VfxContext, Task> onBeforeHit)
    {
        _beforeHitEventList.Add(onBeforeHit);
        return this;
    }

    public VfxBuilder<T> SetOnHit(Func<T, VfxContext, Task> onHit)
    {
        _hitEventList = [onHit];
        return this;
    }

    public VfxBuilder<T> AddOnHit(Func<T, VfxContext, Task> onHit)
    {
        _hitEventList.Add(onHit);
        return this;
    }

    public VfxBuilder<T> SetOnAfterHit(Func<T, VfxContext, Task> onAfterHit)
    {
        _afterHitEventList = [onAfterHit];
        return this;
    }

    public VfxBuilder<T> AddOnAfterHit(Func<T, VfxContext, Task> onAfterHit)
    {
        _afterHitEventList.Add(onAfterHit);
        return this;
    }

    public VfxBuilder<T> SetOnFinish(Func<T, VfxContext, Task> onFinish)
    {
        _finishEventList = [onFinish];
        return this;
    }

    public VfxBuilder<T> AddOnFinish(Func<T, VfxContext, Task> onFinish)
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

    public IEnumerable<T> Create()
    {
        for (var i = 0; i < _repeatCount; i++)
        {
            yield return InstantiateVfx(new VfxContext());
        }
    }

    public async Task Emit(float eachDelay = 0.0f, int groupCount = 0, float groupDelay = 0.0f)
    {
        if (groupCount < 0)
        {
            groupCount = 0;
        }

        for (var i = 0; i < _repeatCount; i++)
        {
            var vfx = InstantiateVfx(new VfxContext());
            Spawn(vfx);

            if (groupCount > 0)
            {
                var isNewGroup = i > 0 && i % groupCount == 0;
                await Cmd.Wait(isNewGroup ? groupDelay : eachDelay);
            }
            else
            {
                await Cmd.Wait(eachDelay);
            }
        }
    }

    private T InstantiateVfx(VfxContext vfxContext)
    {
        var vfx = Scene.Instantiate<T>();
        WireEvents(vfx, vfxContext);
        return vfx;
    }

    private static void Spawn(NBangDreamVfx node)
    {
        var vfxContainer = NCombatRoom.Instance?.CombatVfxContainer;
        if (vfxContainer != null)
            vfxContainer.AddChildSafely(node);
        else
            NRun.Instance?.GlobalUi.AddChildSafely(node);
    }
}

public class VfxContext
{
    private readonly Dictionary<string, object> _data = new();

    public void Set<T>(string key, T value) => _data[key] = value!;
    public T? Get<T>(string key) => _data.TryGetValue(key, out var v) ? (T?)v : default;
    public void Remove(string key) => _data.Remove(key);
    public void Clear() => _data.Clear();

    internal VfxContext()
    {
    }
}