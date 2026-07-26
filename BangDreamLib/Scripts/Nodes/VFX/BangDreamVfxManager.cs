using System.Reflection;
using BangDreamLib.Scripts.Enums;
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

    private readonly Dictionary<NBangDreamFlyingVfx, Action> _treeExitedActions = new();

    private readonly Dictionary<NBangDreamFlyingVfx, List<(StringName, Callable)>> _connectedCallables = new();

    private readonly Dictionary<NBangDreamFlyingVfx, VfxHandle> _handles = new();

    private readonly HashSet<NBangDreamFlyingVfx> _activeVfx = [];

    private Control? _parent;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        foreach (var vfx in _activeVfx.ToArray())
            UnregisterVfx(vfx, VfxResult.Cancelled);

        _treeExitedActions.Clear();
        _connectedCallables.Clear();
        _handles.Clear();

        _activeVfx.Clear();

        Instance = null;
    }

    public override void _Ready()
    {
        _parent = GetParent<Control>();
    }

    public VfxHandle SubmitVfx(NBangDreamFlyingVfx flyingVfx)
    {
        ArgumentNullException.ThrowIfNull(flyingVfx);

        if (!_activeVfx.Add(flyingVfx))
            return _handles[flyingVfx];

        var handle = new VfxHandle(flyingVfx.Context);
        _handles[flyingVfx] = handle;

        var connections = new List<(StringName, Callable)>
        {
            (NBangDreamFlyingVfx.SignalName.HitTriggered,
                Callable.From<VfxContext>(_ => handle.CompleteArrival(VfxResult.Arrived))),
            (NBangDreamFlyingVfx.SignalName.VfxFinished,
                Callable.From<VfxContext>(_ =>
                {
                    var result = CombatManager.Instance.IsInProgress
                        ? VfxResult.Finished
                        : VfxResult.CombatEnded;
                    handle.Complete(result);
                })),
        };

        RegisterVfx(flyingVfx, connections);
        return handle;
    }

    public void UnregisterVfx(NBangDreamFlyingVfx? vfx, VfxResult result = VfxResult.NodeRemoved)
    {
        if (vfx == null || !_activeVfx.Contains(vfx)) return;

        if (_treeExitedActions.Remove(vfx, out var treeAction))
        {
            if (IsInstanceValid(vfx))
                vfx.TreeExited -= treeAction;
        }

        if (_connectedCallables.Remove(vfx, out var connections))
        {
            if (IsInstanceValid(vfx))
            {
                foreach (var (signal, callable) in connections)
                    vfx.Disconnect(signal, callable);
            }
        }

        if (_handles.Remove(vfx, out var handle))
        {
            handle.Complete(result);
        }

        _activeVfx.Remove(vfx);
    }

    internal static void NotifyCombatStateChanged()
    {
        if (CombatManager.Instance.IsInProgress)
            StateChanged?.Invoke(CombatManager.Instance.StateTracker, ["BangDreamVfx"]);
    }

    private void RegisterVfx(NBangDreamFlyingVfx flyingVfx, List<(StringName, Callable)> connections)
    {
        foreach (var (signal, callable) in connections)
            flyingVfx.Connect(signal, callable);

        _connectedCallables[flyingVfx] = connections;

        var onTreeExited = () =>
        {
            var result = CombatManager.Instance.IsInProgress
                ? VfxResult.NodeRemoved
                : VfxResult.CombatEnded;
            UnregisterVfx(flyingVfx, result);
        };
        _treeExitedActions[flyingVfx] = onTreeExited;
        flyingVfx.TreeExited += onTreeExited;

        try
        {
            var container = _parent ?? this;
            container.AddChildSafely(flyingVfx);
        }
        catch
        {
            UnregisterVfx(flyingVfx, VfxResult.Cancelled);
            throw;
        }
    }
}