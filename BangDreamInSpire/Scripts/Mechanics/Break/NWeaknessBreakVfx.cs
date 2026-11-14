using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamInSpire.Scripts.Mechanics.Break;

/// <summary>
/// 破绽 / 弱点击破的常驻视觉（方案 A「裂纹积累」，2026-11-14 批准）。
/// <para>
/// 挂在目标 <see cref="NCreature" /> 上（节点名 <see cref="NodeName" />）：
/// 破绽 1–3 层 = 命中框上缘 n 枚菱形破绽点 + 逐层增多的细裂纹；
/// 满 4 层进入弱点击破 = 整块碎裂贴图压在目标身上（一次性 0.75 → 持续 0.22–0.34 呼吸）+ 碎片闪光，
/// 四点转红；退出时整体淡出后自毁。Hover 时弹出与能力同款的 tips。
/// </para>
/// <para>
/// 外部只依赖 <see cref="Sync" />、<see cref="Refresh" />、<see cref="FadeOut" /> 与 <see cref="NodeName" />；
/// 设计契约见 <c>.scratch/vfx/plans/mutsumi-weakness-break.md</c>。
/// </para>
/// </summary>
public sealed partial class NWeaknessBreakVfx : Control
{
    /// <summary>挂在 creature 节点下的固定节点名，供刷新时查找复用。</summary>
    public const string NodeName = "WeaknessBreakVfx";

    /// <summary>碎裂纹理（LOCAL 素材，uuid 见计划 §7.1）。</summary>
    private const string CrackTexturePath =
        "res://BangDreamInSpire/images/vfx/weakness_break/weakness_break_cracks.png";

    /// <summary>碎片闪光（本体资源，按 BODY 引用）。</summary>
    private const string ShatterTexturePath =
        "res://images/vfx/vfx_rock_shatter/vfx_rock_shatter_shine_01.png";

    private const float StripHeight = 20f;
    private const float BadgeRadius = 5f;
    private const float BadgeSpacing = 24f;
    private const float StackCrackAlpha = 0.35f;
    private const float BrokenHoldMin = 0.22f;
    private const float BrokenHoldMax = 0.34f;
    private const float BrokenEnterAlpha = 0.75f;
    private const float BrokenEnterDuration = 0.105f;
    private const float ShatterDuration = 0.135f;
    private const float ShatterStartSize = 230f;
    private const float ShatterEndSize = 260f;
    private const float CrackMinSize = 180f;
    private const float CrackMaxSize = 320f;
    private const float FadeOutDuration = 0.35f;
    private const float PulseAngularSpeed = 5.2f;
    private const int StackCrackPointCount = 4;

    private static readonly Color NormalOutline = new("#102434");
    private static readonly Color NormalBody = new("#8fd4ff");
    private static readonly Color NormalCore = new("#f2fbff");
    private static readonly Color CrackLine = new("#eaf9ff");
    private static readonly Color BrokenOutline = new("#2a0b12");
    private static readonly Color BrokenBody = new("#ff5a6e");
    private static readonly Color BrokenCore = new("#ffe0a6");
    private static readonly Color BrokenTint = new("#ffc2ca");

    private NCreature? _ownerNode;
    private Creature? _creature;
    private Tween? _fadeTween;
    private Texture2D? _crackTexture;
    private Texture2D? _shatterTexture;
    private int _stacks;
    private bool _broken;
    private bool _fading;
    private float _pulsePhase;
    private float _enterElapsed = float.MaxValue;
    private float _hitboxWidth = 72f;
    private float _hitboxHeight = 60f;

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
        _crackTexture = BangDreamPreloadManager.GetTexture2D(CrackTexturePath);
        _shatterTexture = BangDreamPreloadManager.GetTexture2D(ShatterTexturePath);
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
        if (_fading)
        {
            return;
        }

        var changed = false;
        if (_enterElapsed < BrokenEnterDuration + ShatterDuration)
        {
            _enterElapsed += (float)delta;
            changed = true;
        }

        if (_broken)
        {
            _pulsePhase += (float)delta * PulseAngularSpeed;
            changed = true;
        }

        if (changed)
        {
            QueueRedraw();
        }
    }

    /// <summary>外部唯一刷新入口：按层数与是否弱点击破重画。进入弱点击破时重置一次性计时。</summary>
    public void Refresh(int stacks, bool broken)
    {
        var enteringBreak = broken && !_broken;
        _stacks = Mathf.Clamp(stacks, 0, BreakConst.MaxStacks);
        _broken = broken;
        _fading = false;
        KillFadeTween();
        Modulate = Colors.White;
        Visible = true;

        if (enteringBreak)
        {
            _enterElapsed = 0f;
            _pulsePhase = 0f;
        }

        LayoutFromHitbox();
        SetProcess(true);
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
        var hitboxHeight = hitbox?.Size.Y ?? 0f;
        _hitboxWidth = hitboxWidth > 24f ? hitboxWidth : 72f;
        _hitboxHeight = hitboxHeight > 24f ? hitboxHeight : 60f;

        var left = hitbox?.Position.X ?? 0f;
        var top = hitbox?.Position.Y ?? 0f;

        Position = new Vector2(left + (hitboxWidth - _hitboxWidth) * 0.5f, top - StripHeight - 6f);
        Size = new Vector2(_hitboxWidth, StripHeight);
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

        DrawPips();

        if (_broken)
        {
            DrawBrokenForm();
        }
        else
        {
            DrawStackCracks();
        }
    }

    /// <summary>命中框上缘的层数点：1–3 层为冷白，弱点击破时为 4 枚红色。</summary>
    private void DrawPips()
    {
        var count = _broken ? BreakConst.MaxStacks : Mathf.Clamp(_stacks, 1, BreakConst.MaxStacks);
        var centerY = Size.Y * 0.5f;
        var centerX = Size.X * 0.5f;
        var body = _broken ? BrokenBody : NormalBody;
        var core = _broken ? BrokenCore : NormalCore;
        var outline = _broken ? BrokenOutline : NormalOutline;
        // 层数变化时 0.12s 的 pop：进入弱点击破时用同一条曲线放大
        var pop = _enterElapsed < 0.12f
            ? 1f + 0.25f * Mathf.Sin(Mathf.Clamp(_enterElapsed / 0.12f, 0f, 1f) * Mathf.Pi)
            : 1f;

        for (var i = 0; i < count; i++)
        {
            var offset = (i - (count - 1) * 0.5f) * BadgeSpacing;
            var center = new Vector2(centerX + offset, centerY);
            DrawColoredPolygon(Diamond(center, (BadgeRadius + 2f) * pop), outline);
            DrawColoredPolygon(Diamond(center, (BadgeRadius + 0.5f) * pop), body);
            DrawCircle(center, BadgeRadius * 0.45f * pop, core);
        }
    }

    /// <summary>1–3 层：每层一条从破绽点爬进身体的细裂纹（主干 + 一个分叉）。</summary>
    private void DrawStackCracks()
    {
        var bodyTop = Size.Y + 2f;
        var bodyCenterX = Size.X * 0.5f;
        var count = Mathf.Clamp(_stacks, 1, BreakConst.MaxStacks);

        for (var stack = 0; stack < count; stack++)
        {
            var seedOffset = stack * 0.37f;
            var startX = bodyCenterX + Mathf.Sin(seedOffset * Mathf.Tau) * Size.X * 0.28f;
            var points = new Vector2[StackCrackPointCount];
            for (var i = 0; i < StackCrackPointCount; i++)
            {
                var t = (float)i / (StackCrackPointCount - 1);
                var wobble = Mathf.Sin((seedOffset + t * 1.7f) * Mathf.Tau) * 9f;
                points[i] = new Vector2(
                    Mathf.Lerp(startX, bodyCenterX + (seedOffset - 0.5f) * 18f, t) + wobble,
                    bodyTop + t * Mathf.Min(46f, _hitboxHeight * 0.42f));
            }

            DrawPolyline(points, new Color(CrackLine, StackCrackAlpha), 2.2f, true);
            // 分叉
            DrawPolyline([points[1], points[1] + new Vector2(10f - stack * 4f, -7f)],
                new Color(CrackLine, StackCrackAlpha * 0.75f), 1.5f, true);
        }
    }

    /// <summary>弱点击破：整块碎裂贴图压在身体上（一次性冲高 → 低透明度呼吸）+ 碎片闪光。</summary>
    private void DrawBrokenForm()
    {
        var bodyCenter = new Vector2(Size.X * 0.5f, Size.Y + _hitboxHeight * 0.45f);
        var crackSize = Mathf.Clamp(_hitboxWidth * 0.9f, CrackMinSize, CrackMaxSize);

        if (_crackTexture != null)
        {
            float alpha;
            if (_enterElapsed < BrokenEnterDuration)
            {
                var t = _enterElapsed / BrokenEnterDuration;
                alpha = Mathf.Lerp(0f, BrokenEnterAlpha, 1f - Mathf.Pow(1f - t, 3f));
            }
            else
            {
                var pulse = 0.5f + 0.5f * Mathf.Sin(_pulsePhase);
                alpha = Mathf.Lerp(BrokenHoldMin, BrokenHoldMax, pulse);
            }

            var scale = _enterElapsed < BrokenEnterDuration
                ? Mathf.Lerp(0.85f, 1.05f, Mathf.Clamp(_enterElapsed / BrokenEnterDuration, 0f, 1f))
                : 1f;
            var size = new Vector2(crackSize, crackSize) * scale;
            DrawTextureRect(_crackTexture, new Rect2(bodyCenter - size / 2f, size), false,
                new Color(BrokenTint, alpha));
        }

        if (_shatterTexture != null && _enterElapsed < ShatterDuration)
        {
            var t = Mathf.Clamp(_enterElapsed / ShatterDuration, 0f, 1f);
            var size = Mathf.Lerp(ShatterStartSize, ShatterEndSize, t);
            DrawSetTransform(bodyCenter, Mathf.DegToRad(10f * t), Vector2.One);
            DrawTextureRect(_shatterTexture, new Rect2(new Vector2(-size, -size) / 2f, new Vector2(size, size)),
                false, new Color(1f, 0.56f, 0.63f, 0.9f * (1f - t)));
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }
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
