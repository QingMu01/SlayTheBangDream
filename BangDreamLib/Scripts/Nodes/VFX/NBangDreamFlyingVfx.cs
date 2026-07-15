using BangDreamLib.Scripts.Enums;
using BangDreamLib.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamLib.Scripts.Nodes.VFX;

public abstract partial class NBangDreamFlyingVfx : Node2D
{
    [Signal]
    public delegate void VfxSpawnedEventHandler(VfxContext vfxContext);

    [Signal]
    public delegate void BeforeHitEventHandler(VfxContext vfxContext);

    [Signal]
    public delegate void HitTriggeredEventHandler(VfxContext vfxContext);

    [Signal]
    public delegate void AfterHitEventHandler(VfxContext vfxContext);

    [Signal]
    public delegate void VfxFinishedEventHandler(VfxContext vfxContext);

    private VfxContext? _context;
    protected bool IsFinished { get; set; }

    public VfxContext Context
    {
        get { return _context ??= new VfxContext(this); }
    }

    public virtual bool UpdateCombatTracker { get; set; } = false;

    protected void EmitSpawnSignal()
    {
        Context.Lifecycle = VfxLifecycle.Spawn;
        EmitSignal(SignalName.VfxSpawned, Context);
    }

    protected void EmitBeforeHitSignal()
    {
        Context.Lifecycle = VfxLifecycle.BeforeHit;
        EmitSignal(SignalName.BeforeHit, Context);
    }

    protected void EmitHitSignal()
    {
        Context.Lifecycle = VfxLifecycle.Hit;
        EmitSignal(SignalName.HitTriggered, Context);
    }

    protected void EmitAfterHitSignal()
    {
        Context.Lifecycle = VfxLifecycle.AfterHit;
        EmitSignal(SignalName.AfterHit, Context);
    }

    protected void EmitFinishSignal()
    {
        if (IsFinished) return;
        IsFinished = true;
        Context.Lifecycle = VfxLifecycle.Finish;
        EmitSignal(SignalName.VfxFinished, Context);

        CallDeferred(nameof(Destroy));
    }

    private void Destroy()
    {
        if (IsInstanceValid(this) && Context.Lifecycle == VfxLifecycle.Finish)
        {
            this.QueueFreeSafely();
        }
    }
}