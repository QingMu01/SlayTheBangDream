using BangDreamLib.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamLib.Scripts.Nodes.VFX;

public abstract partial class NBangDreamVfx : Node2D
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
        EmitSignal(SignalName.VfxSpawned, Context);
    }

    protected void EmitBeforeHitSignal()
    {
        EmitSignal(SignalName.BeforeHit, Context);
    }

    protected void EmitHitSignal()
    {
        EmitSignal(SignalName.HitTriggered, Context);
    }

    protected void EmitAfterHitSignal()
    {
        EmitSignal(SignalName.AfterHit, Context);
    }

    protected void EmitFinishSignal()
    {
        if (IsFinished) return;
        IsFinished = true;
        EmitSignal(SignalName.VfxFinished, Context);

        CallDeferred(nameof(Destroy));
    }

    private void Destroy()
    {
        this.QueueFreeSafely();
    }
}