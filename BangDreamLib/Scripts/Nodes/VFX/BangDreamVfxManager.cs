using System.Reflection;
using BangDreamLib.Scripts.Interfaces;
using BangDreamLib.Scripts.Utils.Infos;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamLib.Scripts.Nodes.VFX;

public partial class BangDreamVfxManager : Control
{
    private static readonly MethodInfo? StateChanged =
        AccessTools.Method(typeof(CombatStateTracker), "NotifyCombatStateChanged");

    public static BangDreamVfxManager? Instance { get; private set; }

    private readonly Dictionary<NBangDreamVfx, Action> _treeExitedActions = new();

    private readonly Dictionary<NBangDreamVfx, List<(StringName signal, Callable callable)>>
        _connectedCallables = new();

    private readonly HashSet<NBangDreamVfx> _activeVfx = [];

    private Control? _parent;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        foreach (var vfx in _activeVfx.ToArray())
            UnregisterVfx(vfx);

        _treeExitedActions.Clear();
        _connectedCallables.Clear();

        _activeVfx.Clear();

        Instance = null;
    }

    public override void _Ready()
    {
        _parent = GetParent<Control>();
    }

    public void SubmitVfx(NBangDreamVfx vfx, IVfxEffectHandler handler)
    {
        if (!_activeVfx.Add(vfx)) return;

        var connections = new List<(StringName, Callable)>
        {
            (NBangDreamVfx.SignalName.VfxSpawned, ToCallable(handler.OnSpawn)),
            (NBangDreamVfx.SignalName.BeforeHit, ToCallable(handler.OnBeforeHit)),
            (NBangDreamVfx.SignalName.HitTriggered, ToCallable(handler.OnHit)),
            (NBangDreamVfx.SignalName.AfterHit, ToCallable(handler.OnAfterHit)),
            (NBangDreamVfx.SignalName.VfxFinished, ToCallable(handler.OnFinish)),
        };

        foreach (var (signal, callable) in connections)
            vfx.Connect(signal, callable);

        _connectedCallables[vfx] = connections;

        var onTreeExited = () => UnregisterVfx(vfx);
        _treeExitedActions[vfx] = onTreeExited;
        vfx.TreeExited += onTreeExited;

        var container = _parent ?? this;
        container.AddChildSafely(vfx);
    }


    public void UnregisterVfx(NBangDreamVfx? vfx)
    {
        if (!IsInstanceValid(vfx) || !_activeVfx.Contains(vfx)) return;

        if (_treeExitedActions.Remove(vfx, out var treeAction))
        {
            vfx.TreeExited -= treeAction;
        }

        if (_connectedCallables.Remove(vfx, out var connections))
        {
            foreach (var (signal, callable) in connections)
                vfx.Disconnect(signal, callable);
        }

        if (vfx.UpdateCombatTracker)
        {
            StateChanged?.Invoke(CombatManager.Instance.StateTracker, ["FakeInvoke"]);
        }

        _activeVfx.Remove(vfx);
    }

    private static Callable ToCallable(Func<VfxContext, Task> asyncAction)
    {
        return Callable.From<VfxContext>(context => { _ = SafeInvokeAsync(asyncAction, context); });
    }

    private static async Task SafeInvokeAsync(Func<VfxContext, Task> asyncAction, VfxContext context)
    {
        try
        {
            await asyncAction(context);
        }
        catch (Exception e)
        {
            BangDreamLibCore.Logger.Error($"VfxManager dispatch error: {e}");
        }
    }
}