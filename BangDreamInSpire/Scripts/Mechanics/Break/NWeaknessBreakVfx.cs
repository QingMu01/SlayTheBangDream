using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace BangDreamInSpire.Scripts.Mechanics.Break;

/// <summary>
/// 破绽 / 弱点击破的常驻视觉（本轮为**占位**实现，纯程序化绘制，无美术素材）。
/// <para>
/// 挂在目标 <see cref="NCreature" /> 上（直接 <c>AddChild</c> 到 creature 节点，节点名固定为
/// <see cref="NodeName" />），沿命中框上缘显示：
/// 破绽 1–3 层 = n 枚指示点；4 层 / 弱点击破 = 「破裂」形态（换色 + 轻微脉冲）。
/// Hover 时弹出与能力同款的 tips 框（<see cref="HoverTipFactory.FromPower" />）。
/// </para>
/// <para>
/// 正式特效替换契约（外部只依赖这两个入口与 <see cref="NodeName" />）：
/// <see cref="Refresh" /> 与 <see cref="FadeOut" />。替换内层视觉时只改
/// <c>_Draw</c> / <c>_Process</c> 与子节点命名，不改静态入口。
/// </para>
/// </summary>
public sealed partial class NWeaknessBreakVfx : Control
{
    /// <summary>挂在 creature 节点下的固定节点名，供刷新时查找复用。</summary>
    public const string NodeName = "WeaknessBreakVfx";

    private const float StripHeight = 20f;
    private const float BadgeRadius = 5f;
    private const float BadgeSpacing = 16f;
    private const float FadeOutDuration = 0.35f;
    private const float PulseAngularSpeed = 5.2f;
    private const int BrokenSegmentCount = 8;

    private static readonly Color NormalOutline = new("#102434");
    private static readonly Color NormalBody = new("#8fd4ff");
    private static readonly Color NormalCore = new("#f2fbff");
    private static readonly Color BrokenOutline = new("#2a0b12");
    private static readonly Color BrokenBody = new("#ff4d6a");
    private static readonly Color BrokenCore = new("#ffe0a6");

    private NCreature? _ownerNode;
    private Creature? _creature;
    private Tween? _fadeTween;
    private int _stacks;
    private bool _broken;
    private bool _fading;
    private float _pulsePhase;

    /// <summary>当前显示的破绽层数（0–4）。</summary>
    public int Stacks => _stacks;

    /// <summary>当前是否显示「弱点击破」形态。</summary>
    public bool IsBroken => _broken;

    /// <summary>
    /// 把目标的视觉同步到它的实际状态：没有破绽且不在弱点击破时淡出，否则创建/刷新。
    /// 由 Power 的生命周期与 <see cref="BreakCmd" /> 调用，重入安全。
    /// </summary>
    public static void Sync(Creature? creature)
    {
        if (creature == null)
        {
            return;
        }

        var node = NCombatRoom.Instance?.GetCreatureNode(creature);
        if (node == null || !IsInstanceValid(node))
        {
            // 战斗节点尚未构建（或已经销毁）：等下一次状态变化时再补建，不在这里排队。
            return;
        }

        var stacks = creature.GetPowerAmount<WeaknessBreakPower>();
        var broken = creature.GetPower<WeakPointBreakPower>() != null;
        var existing = node.GetNodeOrNull<NWeaknessBreakVfx>(NodeName);

        if (stacks <= 0 && !broken)
        {
            existing?.FadeOut();
            return;
        }

        if (existing == null || existing.IsQueuedForDeletion())
        {
            existing = Create(node, creature);
        }

        existing.Refresh(stacks, broken);
    }

    private static NWeaknessBreakVfx Create(NCreature node, Creature creature)
    {
        var vfx = new NWeaknessBreakVfx
        {
            Name = NodeName,
            _ownerNode = node,
            _creature = creature,
            // Pass：仍然收到 MouseEntered/Exited（Hover 弹 tips），但不吞掉点击与拖拽，
            // 避免挡住落在 creature 上缘上方的选牌/拖放输入。
            MouseFilter = MouseFilterEnum.Pass,
            FocusMode = FocusModeEnum.None,
        };
        node.AddChild(vfx);
        return vfx;
    }

    public override void _Ready()
    {
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
        LayoutFromHitbox();
    }

    public override void _ExitTree()
    {
        NHoverTipSet.Remove(this);
        KillFadeTween();
    }

    public override void _Process(double delta)
    {
        if (!_broken || _fading)
        {
            return;
        }

        _pulsePhase += (float)delta * PulseAngularSpeed;
        QueueRedraw();
    }

    /// <summary>外部唯一刷新入口：按层数与是否弱点击破重画。</summary>
    public void Refresh(int stacks, bool broken)
    {
        _stacks = Mathf.Clamp(stacks, 0, BreakConst.MaxStacks);
        _broken = broken;
        _fading = false;
        KillFadeTween();
        Modulate = Colors.White;
        Visible = true;
        LayoutFromHitbox();
        SetProcess(_broken);
        QueueRedraw();
    }

    /// <summary>外部唯一移除入口：淡出后自毁；淡出过程中被 Refresh 打断则复活。</summary>
    public void FadeOut()
    {
        if (_fading || IsQueuedForDeletion())
        {
            return;
        }

        _fading = true;
        SetProcess(false);
        NHoverTipSet.Remove(this);
        KillFadeTween();

        if (!IsInsideTree())
        {
            QueueFree();
            return;
        }

        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(this, "modulate:a", 0f, FadeOutDuration);
        _fadeTween.TweenCallback(Callable.From(FreeSelf));
    }

    private void FreeSelf()
    {
        _fadeTween = null;
        if (IsInstanceValid(this))
        {
            QueueFree();
        }
    }

    private void KillFadeTween()
    {
        if (_fadeTween != null && IsInstanceValid(_fadeTween))
        {
            _fadeTween.Kill();
        }

        _fadeTween = null;
    }

    /// <summary>沿命中框上缘摆放（本节点是 NCreature 的子节点，坐标即 creature 控件局部坐标）。</summary>
    private void LayoutFromHitbox()
    {
        var hitbox = _ownerNode?.Hitbox;
        var hitboxWidth = hitbox?.Size.X ?? 0f;
        var width = hitboxWidth > 24f ? hitboxWidth : 72f;
        var left = hitbox?.Position.X ?? 0f;
        var top = hitbox?.Position.Y ?? 0f;

        Position = new Vector2(left + (hitboxWidth - width) * 0.5f, top - StripHeight - 6f);
        Size = new Vector2(width, StripHeight);
    }

    private void OnMouseEntered()
    {
        if (_fading)
        {
            return;
        }

        var tips = CollectHoverTips();
        if (tips.Count == 0)
        {
            return;
        }

        NHoverTipSet.CreateAndShow(this, tips, HoverTip.GetHoverTipAlignment(this, 0.5f));
    }

    private void OnMouseExited()
    {
        NHoverTipSet.Remove(this);
    }

    /// <summary>能力同款的 tips：破绽本身 + （处于弱点击破时）弱点击破状态。</summary>
    private List<IHoverTip> CollectHoverTips()
    {
        var tips = new List<IHoverTip>();
        var creature = _creature;
        if (creature == null)
        {
            return tips;
        }

        if (creature.GetPower<WeaknessBreakPower>() is { } weaknessBreak)
        {
            tips.Add(HoverTipFactory.FromPower(weaknessBreak));
        }

        if (creature.GetPower<WeakPointBreakPower>() is { } weakPointBreak)
        {
            tips.Add(HoverTipFactory.FromPower(weakPointBreak));
        }

        return tips;
    }

    public override void _Draw()
    {
        if (_stacks <= 0 && !_broken)
        {
            return;
        }

        if (_broken)
        {
            DrawBrokenForm();
            return;
        }

        DrawStackForm();
    }

    /// <summary>1–3 层：沿上缘等距排列的 n 枚指示点。</summary>
    private void DrawStackForm()
    {
        var count = Mathf.Max(_stacks, 1);
        var centerY = Size.Y * 0.5f;
        var centerX = Size.X * 0.5f;
        for (var i = 0; i < count; i++)
        {
            var offset = (i - (count - 1) * 0.5f) * BadgeSpacing;
            var center = new Vector2(centerX + offset, centerY);
            DrawColoredPolygon(Diamond(center, BadgeRadius + 2f), NormalOutline);
            DrawColoredPolygon(Diamond(center, BadgeRadius + 0.5f), NormalBody);
            DrawCircle(center, BadgeRadius * 0.45f, NormalCore);
        }
    }

    /// <summary>4 层 / 弱点击破：一枚横贯的破裂锯齿轮廓 + 两端裂点，轻微脉冲。</summary>
    private void DrawBrokenForm()
    {
        var pulse = 0.72f + 0.28f * Mathf.Sin(_pulsePhase);
        var bodyAlpha = Mathf.Clamp(pulse, 0f, 1f);
        var body = new Color(BrokenBody.R, BrokenBody.G, BrokenBody.B, BrokenBody.A * bodyAlpha);
        var core = new Color(BrokenCore.R, BrokenCore.G, BrokenCore.B, BrokenCore.A * bodyAlpha);
        var centerY = Size.Y * 0.5f;
        var left = 6f;
        var right = Mathf.Max(left + 8f, Size.X - 6f);

        // 锯齿折线：上下来回，两端收窄，读作「裂开的一条缝」。
        var crackPoints = new Vector2[BrokenSegmentCount + 1];
        for (var i = 0; i <= BrokenSegmentCount; i++)
        {
            var t = (float)i / BrokenSegmentCount;
            var taper = 1f - Mathf.Abs(t - 0.5f) * 1.2f;
            var amplitude = Mathf.Max(1.5f, (Size.Y * 0.2f) * taper);
            crackPoints[i] = new Vector2(
                Mathf.Lerp(left, right, t),
                centerY + (i % 2 == 0 ? -amplitude : amplitude));
        }

        DrawPolyline(crackPoints, BrokenOutline, 6f, true);
        DrawPolyline(crackPoints, body, 3f, true);
        DrawPolyline(crackPoints, core, 1f, true);
        DrawColoredPolygon(Diamond(new Vector2(left, centerY), 5.5f), body);
        DrawColoredPolygon(Diamond(new Vector2(right, centerY), 5.5f), body);
    }

    private static Vector2[] Diamond(Vector2 center, float radius)
    {
        return
        [
            center + new Vector2(0f, -radius),
            center + new Vector2(radius, 0f),
            center + new Vector2(0f, radius),
            center + new Vector2(-radius, 0f),
        ];
    }
}
