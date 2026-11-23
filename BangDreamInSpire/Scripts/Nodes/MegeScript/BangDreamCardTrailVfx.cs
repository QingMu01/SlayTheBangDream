using System.Reflection;
using BangDreamInSpire.Scripts.Utils;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace BangDreamInSpire.Scripts.Nodes.MegeScript;

[Tool]
public partial class BangDreamCardTrailVfx : NCardTrailVfx
{
    private const float PreviewLegDuration = 1.1f;

    /// <summary>反射访问基类跟随目标。</summary>
    private static readonly FieldInfo? NodeToFollowField =
        AccessTools.Field(typeof(NCardTrailVfx), "_nodeToFollow");

    private Control? _previewTarget;
    private Tween? _previewTween;
    private bool _baseInitialized;

    [Export]
    public bool Play
    {
        get => false;
        set
        {
            if (value)
                BeginPreview();
        }
    }

    public override void _Ready()
    {
        // 编辑器保持静止，F6 或 Play 才初始化。
        if (Engine.IsEditorHint())
        {
            ProcessMode = ProcessModeEnum.Disabled;
            return;
        }

        // F6 创建预览跟随目标后再初始化。
        if (VfxPreviewSupport.IsPreviewRun(this))
            BeginPreview();

        StartTrail();
    }

    /// <summary>幂等执行基类初始化。</summary>
    private void StartTrail()
    {
        if (_baseInitialized)
            return;

        _baseInitialized = true;
        base._Ready();
    }

    public override void _ExitTree()
    {
        _previewTween?.Kill();
        _previewTween = null;

        if (_previewTarget != null && IsInstanceValid(_previewTarget))
            _previewTarget.QueueFree();

        _previewTarget = null;
        base._ExitTree();
    }

    /// <summary>创建并移动预览跟随目标。</summary>
    public void BeginPreview()
    {
        // Play 时恢复逐帧并初始化。
        ProcessMode = ProcessModeEnum.Inherit;
        EnsurePreviewTarget();
        StartTrail();

        if (_previewTarget != null && _previewTarget.IsInsideTree())
            StartPreviewMotion();
    }

    private void EnsurePreviewTarget()
    {
        if (_previewTarget != null && IsInstanceValid(_previewTarget))
        {
            SetNodeToFollow(_previewTarget);
            return;
        }

        // 跟随目标不能是轨迹根的后代，避免递归漂移。
        // 挂到根节点的父级下，可确保与根处于同一视口（编辑器 2D 视图 / F6 窗口）。
        var host = GetParent();
        if (host == null)
            return;

        var target = new Control
        {
            Name = "CardTrailPreviewTarget",
            Size = Vector2.Zero,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        _previewTarget = target;
        SetNodeToFollow(target);

        // 延迟挂载，预览航程取视口中部。
        var (spanStart, _) = VfxPreviewSupport.PreviewTravelSpan(this);
        Callable.From(() =>
        {
            if (!IsInstanceValid(this) || !IsInstanceValid(target))
                return;

            host.AddChild(target);
            target.GlobalPosition = spanStart;
            StartPreviewMotion();
        }).CallDeferred();
    }

    private void StartPreviewMotion()
    {
        if (_previewTarget == null)
            return;

        _previewTween?.Kill();

        // 在预览航程两端往返。
        var (spanStart, spanEnd) = VfxPreviewSupport.PreviewTravelSpan(this);
        _previewTarget.GlobalPosition = spanStart;

        _previewTween = CreateTween();
        _previewTween.SetLoops();
        _previewTween.TweenProperty(_previewTarget, "position", spanEnd, PreviewLegDuration);
        _previewTween.TweenProperty(_previewTarget, "position", spanStart, PreviewLegDuration);
    }

    private void SetNodeToFollow(Control target)
    {
        NodeToFollowField?.SetValue(this, target);
    }
}
