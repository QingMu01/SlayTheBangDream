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

    private decimal _repeatCount = 1m;

    private List<Func<T, Task>> _spawnEventList = [];
    private List<Func<T, Task>> _beforeHitEventList = [];
    private List<Func<T, Task>> _hitEventList = [];
    private List<Func<T, Task>> _afterHitEventList = [];
    private List<Func<T, Task>> _finishEventList = [];

    public VfxBuilder(string scene)
    {
        Scene = PreloadManager.Cache.GetScene(scene);
    }

    public VfxBuilder(PackedScene scene)
    {
        Scene = scene;
    }

    public VfxBuilder<T> RepeatCount(decimal count)
    {
        _repeatCount = count;
        return this;
    }

    public VfxBuilder<T> SetOnSpawn(Func<T, Task> onSpawn)
    {
        _spawnEventList = [onSpawn];
        return this;
    }

    public VfxBuilder<T> AddOnSpawn(Func<T, Task> onSpawn)
    {
        _spawnEventList.Add(onSpawn);
        return this;
    }

    public VfxBuilder<T> SetOnBeforeHit(Func<T, Task> onBeforeHit)
    {
        _beforeHitEventList = [onBeforeHit];
        return this;
    }

    public VfxBuilder<T> AddOnBeforeHit(Func<T, Task> onBeforeHit)
    {
        _beforeHitEventList.Add(onBeforeHit);
        return this;
    }

    public VfxBuilder<T> SetOnHit(Func<T, Task> onHit)
    {
        _hitEventList = [onHit];
        return this;
    }

    public VfxBuilder<T> AddOnHit(Func<T, Task> onHit)
    {
        _hitEventList.Add(onHit);
        return this;
    }

    public VfxBuilder<T> SetOnAfterHit(Func<T, Task> onAfterHit)
    {
        _afterHitEventList = [onAfterHit];
        return this;
    }

    public VfxBuilder<T> AddOnAfterHit(Func<T, Task> onAfterHit)
    {
        _afterHitEventList.Add(onAfterHit);
        return this;
    }

    public VfxBuilder<T> SetOnFinish(Func<T, Task> onFinish)
    {
        _finishEventList = [onFinish];
        return this;
    }

    public VfxBuilder<T> AddOnFinish(Func<T, Task> onFinish)
    {
        _finishEventList.Add(onFinish);
        return this;
    }

    private void WireEvents(T vfx)
    {
        foreach (var func in _spawnEventList)
        {
            vfx.VfxSpawned += async () => await func(vfx);
        }

        foreach (var func in _beforeHitEventList)
        {
            vfx.BeforeHit += async () => await func(vfx);
        }

        foreach (var func in _hitEventList)
        {
            vfx.HitTriggered += async () => await func(vfx);
        }

        foreach (var func in _afterHitEventList)
        {
            vfx.AfterHit += async () => await func(vfx);
        }

        foreach (var func in _finishEventList)
        {
            vfx.VfxFinished += async () => await func(vfx);
        }
    }

    public IEnumerable<T> Create()
    {
        for (var i = 0; i < _repeatCount; i++)
        {
            var vfx = Scene.Instantiate<T>();
            WireEvents(vfx);
            yield return vfx;
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
            var vfx = Scene.Instantiate<T>();
            WireEvents(vfx);
            Spawn(vfx);

            if (groupCount > 0)
            {
                if (i != 0 && i % groupCount == 0)
                {
                    await Cmd.Wait(groupDelay);
                }
                else
                {
                    await Cmd.Wait(eachDelay);
                }
            }
            else
            {
                await Cmd.Wait(eachDelay);
            }
        }
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