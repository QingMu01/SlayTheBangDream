using Godot;

namespace BangDreamLib.Scripts.Nodes.VFX;

public abstract partial class NBangDreamVfx : Node2D
{
    public event Func<Task>? VfxSpawned;

    public event Func<Task>? BeforeHit;

    public event Func<Task>? HitTriggered;

    public event Func<Task>? AfterHit;

    public event Func<Task>? VfxFinished;

    public bool IsFinished { get; protected set; }

    protected async Task TriggerSpawn()
    {
        if (VfxSpawned != null)
        {
            foreach (var @delegate in VfxSpawned.GetInvocationList())
            {
                var handler = (Func<Task>)@delegate;
                try
                {
                    await handler();
                }
                catch (Exception e)
                {
                    BangDreamLibCore.Logger.Error($"Error in VfxSpawned event handler:{e}");
                }
            }
        }
    }

    protected async Task TriggerHit()
    {
        if (BeforeHit != null)
        {
            foreach (var @delegate in BeforeHit.GetInvocationList())
            {
                var handler = (Func<Task>)@delegate;
                try
                {
                    await handler();
                }
                catch (Exception e)
                {
                    BangDreamLibCore.Logger.Error($"Error in BeforeHit event handler:{e}");
                }
            }
        }

        if (HitTriggered != null)
        {
            foreach (var @delegate in HitTriggered.GetInvocationList())
            {
                var handler = (Func<Task>)@delegate;
                try
                {
                    await handler();
                }
                catch (Exception e)
                {
                    BangDreamLibCore.Logger.Error($"Error in HitTriggered event handler:{e}");
                }
            }
        }

        if (AfterHit != null)
        {
            foreach (var @delegate in AfterHit.GetInvocationList())
            {
                var handler = (Func<Task>)@delegate;
                try
                {
                    await handler();
                }
                catch (Exception e)
                {
                    BangDreamLibCore.Logger.Error($"Error in AfterHit event handler:{e}");
                }
            }
        }
    }

    protected async Task TriggerFinish()
    {
        if (IsFinished) return;
        IsFinished = true;

        if (VfxFinished != null)
        {
            foreach (var @delegate in VfxFinished.GetInvocationList())
            {
                var handler = (Func<Task>)@delegate;
                try
                {
                    await handler();
                }
                catch (Exception e)
                {
                    BangDreamLibCore.Logger.Error($"Error in VfxFinished event handler:{e}");
                }
            }
        }

        QueueFree();
    }
}