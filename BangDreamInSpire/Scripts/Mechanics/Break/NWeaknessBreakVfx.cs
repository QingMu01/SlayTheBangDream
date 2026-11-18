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
/// 设计契约见 <c>docs/mechanic-authoring.md</c> §11 / §12.1。
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
    private const float BadgeRadius = 6.5f;
    private const float BadgeSpacing = 24f;
    private const float StackCrackAlpha = 0.45f;
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
    private Sprite2D? _crackSprite;
    private Sprite2D? _shatterSprite;
    private int _stacks;
    private bool _broken;
    private bool _fading;
    private float _pulsePhase;
    private float _enterElapsed = float.MaxValue;
    private float _hitboxWidth = 72f;
    private float _hitboxHeight = 60f;
    private bool _previewMode;
    private float _previewTimer = -1f;
    private int _previewPhase = -1;

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

        // 碎裂贴图是黑底图：必须走加法混合，否则会盖住下层、并在画面上留下一个不透明方块。
        _crackSprite = CreateAdditiveSprite("CrackOverlay", _crackTexture);
        _shatterSprite = CreateAdditiveSprite("ShatterFlash", _shatterTexture);

        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;

        // F6 单独运行场景：没有 Creature 可挂，走一条脚本时间线（1/2/3 层 → 弱点击破 → 清空）自播。
        if (VfxPreviewSupport.IsPreviewRun(this) && _ownerNode == null)
        {
            _previewMode = true;
            _hitboxWidth = 120f;
            _hitboxHeight = 140f;
            Size = new Vector2(_hitboxWidth, StripHeight);
            // Control 不能被 VfxPreviewSupport.CenterForPreview 居中（它只收 Node2D），自己算。
            Position = GetViewportRect().Size * 0.5f - new Vector2(_hitboxWidth * 0.5f, StripHeight + _hitboxHeight * 0.55f);
            _previewTimer = 0f;
        }

        LayoutFromHitbox();
    }

    /// <summary>建一个加法混合的贴图子节点（黑底图直接相加，不遮挡别的层）。</summary>
    private Sprite2D? CreateAdditiveSprite(string name, Texture2D? texture)
    {
        if (texture == null) return null;

        var sprite = new Sprite2D
        {
            Name = name,
            Texture = texture,
            Centered = true,
            Visible = false,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
        };
        AddChild(sprite);
        return sprite;
    }

    /// <summary>把碎裂层与闪光层摆到身体中心（本节点局部坐标）。</summary>
    private void UpdateBrokenSprites()
    {
        if (!_broken)
        {
            // 1–3 层只有层数点 + 细裂纹，不许出现整块碎裂图。
            if (_crackSprite != null) _crackSprite.Visible = false;
            if (_shatterSprite != null) _shatterSprite.Visible = false;
            return;
        }

        var bodyCenter = new Vector2(Size.X * 0.5f, Size.Y + _hitboxHeight * 0.45f);
        var crackSize = Mathf.Clamp(_hitboxWidth * 0.9f, CrackMinSize, CrackMaxSize);

        if (_crackSprite?.Texture is { } crackTexture)
        {
            float alpha;
            if (_enterElapsed < BrokenEnterDuration)
            {
                var t = Mathf.Clamp(_enterElapsed / BrokenEnterDuration, 0f, 1f);
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

            _crackSprite.Position = bodyCenter;
            _crackSprite.Scale = Vector2.One * (crackSize * scale / Mathf.Max(crackTexture.GetWidth(), 1f));
            _crackSprite.Modulate = new Color(BrokenTint, alpha);
            _crackSprite.Visible = true;
        }

        if (_shatterSprite?.Texture is { } shatterTexture)
        {
            var active = _enterElapsed < ShatterDuration;
            _shatterSprite.Visible = active;
            if (active)
            {
                var t = Mathf.Clamp(_enterElapsed / ShatterDuration, 0f, 1f);
                var size = Mathf.Lerp(ShatterStartSize, ShatterEndSize, t);
                _shatterSprite.Position = bodyCenter;
                _shatterSprite.Scale = Vector2.One * (size / Mathf.Max(shatterTexture.GetWidth(), 1f));
                _shatterSprite.Rotation = Mathf.DegToRad(10f * t);
                _shatterSprite.Modulate = new Color(1f, 0.62f, 0.68f, 0.9f * (1f - t));
            }
        }
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

        // 静止态（无脉冲 / 一次性动画已结束 / 非预览）不需要每帧重画：
        // 多目标 AOE 挂多个节点时这是纯帧开销。
        var animating = _broken || _previewMode || _enterElapsed < BrokenEnterDuration + ShatterDuration;
        if (!animating)
        {
            SetProcess(false);
            QueueRedraw();
            return;
        }

        // 一次性计时与脉冲在预览态同样要推进，否则预览会停在「刚进入」的那一帧。
        if (_enterElapsed < BrokenEnterDuration + ShatterDuration)
        {
            _enterElapsed += (float)delta;
        }

        if (_broken)
        {
            _pulsePhase += (float)delta * PulseAngularSpeed;
        }

        if (_previewMode)
        {
            AdvancePreview(delta);
        }

        UpdateBrokenSprites();
        QueueRedraw();
    }

    /// <summary>外部唯一刷新入口：按层数与是否弱点击破重画。进入弱点击破时重置一次性计时。</summary>
    public void Refresh(int stacks, bool broken)
    {
        var enteringBreak = broken && !_broken;
        var stacksChanged = Mathf.Clamp(stacks, 0, BreakConst.MaxStacks) != _stacks;
        _stacks = Mathf.Clamp(stacks, 0, BreakConst.MaxStacks);
        _broken = broken;
        _fading = false;
        KillFadeTween();
        Modulate = Colors.White;
        Visible = true;

        if (enteringBreak || stacksChanged)
        {
            _enterElapsed = 0f;
        }

        if (enteringBreak)
        {
            _pulsePhase = 0f;
        }

        LayoutFromHitbox();
        SetProcess(true);
        if (_crackSprite != null)
        {
            _crackSprite.Visible = _broken;
        }

        UpdateBrokenSprites();
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

    /// <summary>预览时间线：每个阶段停留 1.2s，第 4 阶段进入弱点击破，第 5 阶段清空重来。</summary>
    private void AdvancePreview(double delta)
    {
        _previewTimer += (float)delta;
        var phase = (int)(_previewTimer / 1.2f) % 5;
        if (phase == _previewPhase) return;

        _previewPhase = phase;
        switch (phase)
        {
            case 0:
                Refresh(1, false);
                break;
            case 1:
                Refresh(2, false);
                break;
            case 2:
                Refresh(3, false);
                break;
            case 3:
                Refresh(BreakConst.MaxStacks, true);
                break;
            default:
                _stacks = 0;
                _broken = false;
                _enterElapsed = float.MaxValue;
                QueueRedraw();
                break;
        }
    }

    /// <summary>沿命中框上缘摆放（本节点是 NCreature 的子节点，坐标即 creature 控件局部坐标）。</summary>
    private void LayoutFromHitbox()
    {
        if (_previewMode)
        {
            // 预览态位置由 _Ready 定，不跟着命中框走。
            return;
        }

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

        // 弱点击破时碎裂/闪光是 Sprite2D（加法混合），这里只画层数点。
        if (!_broken)
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
                    bodyTop + t * Mathf.Min(64f, _hitboxHeight * 0.55f));
            }

            DrawPolyline(points, new Color(CrackLine, StackCrackAlpha), 2.8f, true);
            // 分叉
            DrawPolyline([points[1], points[1] + new Vector2(10f - stack * 4f, -7f)],
                new Color(CrackLine, StackCrackAlpha * 0.75f), 1.5f, true);
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
