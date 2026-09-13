using System.Reflection;
using BangDreamLib.Scripts.Utils;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace BangDreamLib.Scripts.Nodes.MegeScript;

[Tool]
public partial class BangDreamCardTrailVfx : NCardTrailVfx
{
    private const float PreviewLegDuration = 1.1f;

    /// <summary>
    /// 基类用于跟随的私有字段。本仓库未公开游戏私有成员（其余代码同样经 AccessTools 访问），
    /// 故这里用反射读写；游戏内该字段由 NCardTrailVfx.Create 注入。
    /// </summary>
    private static readonly FieldInfo? NodeToFollowField =
        AccessTools.Field(typeof(NCardTrailVfx), "_nodeToFollow");

    private Control? _previewTarget;
    private Tween? _previewTween;
    private bool _baseInitialized;

    /// <summary>勾选即重播一次预览轨迹（自动复位，便于反复触发）。</summary>
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
        // 编辑器里完全静止：不执行基类初始化（它会启动跟随与入场补间），逐帧也一并停掉，
        // 场景保持 .tscn 中的作者摆放，便于摆位设计。想查看效果请按 F6，或勾选 Play。
        if (Engine.IsEditorHint())
        {
            ProcessMode = ProcessModeEnum.Disabled;
            return;
        }

        // F6 单独运行时没有外部调用方注入跟随目标，需先造一个再执行基类初始化
        // （基类 _Ready 会对跟随目标做补间，目标为 null 会报错）。
        if (VfxPreviewSupport.IsPreviewRun(this))
            BeginPreview();

        StartTrail();
    }

    /// <summary>执行基类的初始化（幂等）。编辑器里首次点 Play 时才补上。</summary>
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

    /// <summary>
    /// 单独预览入口：造一个傀儡跟随目标并让它来回平移，好让轨迹被拖出来。
    /// 游戏内跟随目标已由 <see cref="NCardTrailVfx.Create" /> 注入，不会进入此分支。
    /// </summary>
    public void BeginPreview()
    {
        // 编辑器里点击 Play 时恢复逐帧并补上基类初始化。
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

        // 傀儡必须是轨迹根节点的「非后代」，否则根跟随傀儡、傀儡又随根移动会形成递归漂移。
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

        // _Ready 期间父节点可能仍在装配子节点，直接 AddChild 会失败，故延迟挂载。
        // 起点用预览航程的 1/4 处，随后向 3/4 处平移，使轨迹出现在视口中部。
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

        // 往返于预览航程两端（x 1/4 ↔ 3/4，y 取中点），让轨迹被完整拖出。
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
