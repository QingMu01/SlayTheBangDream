using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2RitsuLib.Combat.SecondaryResources;
using BangDreamInSpire.Scripts.Mechanics.Lingered;
using BangDreamInSpire.Scripts.Utils;

namespace BangDreamInSpire.Scripts.Nodes.HandPips;

/// <summary>
/// 音乐牌预知：悬停一张会把余音顶到 <see cref="LingeredResourcesRule.HardMaxAmount"/> 的手牌时，
/// 在角色头顶预告即将被抽进歌单的那张曲库牌。取牌口径与溢出结算相同（<c>ExtraDraw.Cards[0]</c>），
/// 一次溢出只抽一张，所以只挂一个卡节点。开关见 <see cref="Config.Precognition"/>。
/// </summary>
internal static class LingeredPrecognition
{
    /// <summary>卡面缩放（相对原型卡原始尺寸）。</summary>
    private const float CardScale = 0.34f;

    /// <summary>头顶浮动幅度（像素）与周期（秒）。</summary>
    private const float BobAmplitude = 2.5f;
    private const double BobPeriod = 2.4;

    /// <summary>跟随的指数插值速率；越大越贴身。</summary>
    private const float FollowRate = 14f;

    private const float PopInTime = 0.18f;
    private const float PopOutTime = 0.15f;

    /// <summary>弹出起始缩放（相对 <see cref="CardScale"/>），与淡入同时完成。</summary>
    private const float PopStartScale = 0.55f;

    /// <summary>淡入结束后的不透明度上限：留一点透明，避免整张卡压住角色。</summary>
    private const float MaxAlpha = 0.95f;

    /// <summary>抬到头顶的距离 = 身高 × <see cref="BodyTopRatio"/> + <see cref="HeadClearance"/>。</summary>
    private const float BodyTopRatio = 0.55f;
    private const float HeadClearance = 96f;

    /// <summary>读不到 <c>%Bounds</c> 时的身高兜底（像素）。</summary>
    private const float DefaultBodyHeight = 220f;

    /// <summary><see cref="Consider"/> 失联时长上限（秒）：超过就自动收起，防止悬停回调不再进来时卡在屏幕上。</summary>
    private const double StaleSeconds = 0.35;

    private static Node2D? _root;
    private static NCreature? _owner;
    private static NCard? _cardNode;

    /// <summary>角色 <c>%Bounds</c>：节点活着时缓存；每帧按字符串路径查找会分配，不能放进 Step。</summary>
    private static Control? _bounds;

    private static CardModel? _card;
    private static double _shownAt;
    private static float _life;
    private static float _fadeOut;
    private static bool _visible;
    private static bool _dying;
    private static double _lastHideLog;
    private static double _lastConsiderAt;

    private static double Now => Time.GetTicksMsec() / 1000.0;

    /// <summary>
    /// 由悬停预测每帧回调：<paramref name="predictedGain"/> 是这张牌打出去会得到的余音量
    /// （休止牌 / 延迟结算时为 0）。低于阈值或拿不到曲库顶张就收起。
    /// </summary>
    internal static void Consider(Player? player, CardModel? hovered, int predictedGain)
    {
        _lastConsiderAt = Now;

        try
        {
            if (!Config.Data.Precognition || hovered == null || player?.Creature == null
                || NCombatRoom.Instance?.GetCreatureNode(player.Creature) is not NCreature node)
            {
                Hide();
                return;
            }

            int current = SecondaryResourceCmd.Get(player, BangDreamConst.LingeredResource);
            int threshold = LingeredResourcesRule.HardMaxAmount;
            if (predictedGain <= 0 || current + predictedGain < threshold)
            {
                Hide($"below({current}+{predictedGain}/{threshold})");
                return;
            }

            var pile = BangDreamConst.ExtraDraw.GetPile(player);
            if (pile == null || pile.Cards.Count == 0)
            {
                Hide("extra-empty");
                return;
            }

            CardModel? top = pile.Cards[0];
            if (top == null)
            {
                Hide("extra-null");
                return;
            }

            // 同一张牌且节点还活着就不重建：重建会打断淡入与浮动，也白丢一个 NCard。
            if (!ReferenceEquals(_card, top) || !IsAlive(_root))
            {
                Build(node, top);
            }

            _visible = true;
            _dying = false;
            _fadeOut = 0f;
        }
        catch (Exception e)
        {
            if (Config.Data.Debug)
            {
                GD.PushWarning($"[BangDreamInSpire/handpips] precog: {e.Message}");
            }
        }
    }

    /// <summary>
    /// 收起预知。<paramref name="reason"/> 只在 Debug 下打印并做了节流：无悬停时调用方每帧都会走这里。
    /// </summary>
    internal static void Hide(string reason = "")
    {
        _visible = false;
        _card = null;

        if (Config.Data.Debug && !string.IsNullOrEmpty(reason))
        {
            double now = Now;
            if (now - _lastHideLog > 1.0)
            {
                _lastHideLog = now;
                GD.Print($"[BangDreamInSpire/handpips] precog hide: {reason}");
            }
        }

        if (IsAlive(_root) && !_dying)
        {
            _dying = true;
            _fadeOut = PopOutTime;
        }
    }

    /// <summary>每帧推进：淡出回收 → 看门狗 → 跟随与淡入。由 <see cref="OrbRenderer"/> 的 Tick 驱动。</summary>
    internal static void Step(float delta)
    {
        Node2D? root = _root;
        if (root == null || !GodotObject.IsInstanceValid(root))
        {
            // 角色节点被回收时子节点一起消失，留在这里的只会是失效指针。
            Reset();
            return;
        }

        if (_dying)
        {
            _fadeOut -= delta;
            ApplyAlpha(Mathf.Clamp(_fadeOut / PopOutTime, 0f, 1f));
            if (_fadeOut <= 0f)
            {
                root.QueueFree();
                Reset();
            }

            return;
        }

        if (!_visible)
        {
            return;
        }

        double now = Now;
        if (_lastConsiderAt > 0 && now - _lastConsiderAt > StaleSeconds)
        {
            Hide("stale");
            return;
        }

        _life += delta;
        float phase = (float)((now - _shownAt) / BobPeriod * Math.PI * 2.0);
        Place(Mathf.Sin(phase) * BobAmplitude, delta);

        float ease = 1f - Mathf.Pow(1f - Mathf.Clamp(_life / PopInTime, 0f, 1f), 3f);
        NCard? cardNode = _cardNode;
        if (cardNode != null && GodotObject.IsInstanceValid(cardNode))
        {
            cardNode.Scale = Vector2.One * (CardScale * Mathf.Lerp(PopStartScale, 1f, ease));
        }

        ApplyAlpha(ease);
    }

    /// <summary>
    /// 换卡：旧节点立即 <c>Free()</c>（不等这一帧结束，免得新旧卡重叠一帧），新节点挂到角色的父节点下。
    /// 任何一步失败都不留半成品节点。
    /// </summary>
    private static void Build(NCreature creature, CardModel card)
    {
        Node2D? previous = _root;
        if (previous != null && GodotObject.IsInstanceValid(previous))
        {
            previous.Free();
        }

        Reset();

        if (!GodotObject.IsInstanceValid(creature) || creature.Visuals == null
            || !GodotObject.IsInstanceValid(creature.Visuals))
        {
            return;
        }

        Node? container = creature.GetParent();
        if (container == null)
        {
            return;
        }

        // 用原型卡（DeckVersion）喂 NCard：直接给曲库实例会渲染成 Broken Card 占位。
        CardModel display = (card.DeckVersion as CardModel) ?? card.CanonicalInstance ?? card;
        var cardNode = NCard.Create(display);
        if (cardNode == null)
        {
            return;
        }

        var root = new Node2D { Name = "LingeredPrecog", TopLevel = true, ZIndex = 60 };
        cardNode.Scale = Vector2.One * (CardScale * PopStartScale);
        cardNode.Modulate = new Color(1f, 1f, 1f, 0f);
        cardNode.ZIndex = 10;

        root.AddChild(cardNode);
        container.AddChild(root);

        _root = root;
        _cardNode = cardNode;
        _owner = creature;
        _bounds = creature.Visuals.GetNodeOrNull<Control>("%Bounds");
        _card = card;
        _shownAt = Now;
        _life = 0f;
        _visible = true;
        _dying = false;
        _fadeOut = 0f;

        Place(0f, 1f);

        if (Config.Data.Debug)
        {
            GD.Print($"[BangDreamInSpire/handpips] precog show: {display.Id.Entry}"
                     + $" (source={card.Id.Entry}, deckVer={card.DeckVersion != null},"
                     + $" pile[{BangDreamConst.ExtraDraw.GetPile(card.Owner).Cards.Count}])");
        }
    }

    /// <summary>解引用：节点已回收或即将回收时清空全部静态状态，别把失效指针留给下一帧。</summary>
    private static void Reset()
    {
        _root = null;
        _cardNode = null;
        _owner = null;
        _bounds = null;
        _card = null;
        _visible = false;
        _dying = false;
        _fadeOut = 0f;
    }

    private static void ApplyAlpha(float k)
    {
        NCard? cardNode = _cardNode;
        if (cardNode != null && GodotObject.IsInstanceValid(cardNode))
        {
            cardNode.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(MaxAlpha * k, 0f, 1f));
        }
    }

    /// <summary>头顶跟随：位置做指数插值，角色移动或受击抖动时卡不会硬跳。</summary>
    private static void Place(float bob, float delta)
    {
        Node2D? root = _root;
        NCreature? owner = _owner;
        if (root == null || !GodotObject.IsInstanceValid(root) || owner == null
            || !GodotObject.IsInstanceValid(owner))
        {
            return;
        }

        var visuals = owner.Visuals;
        if (visuals == null || !GodotObject.IsInstanceValid(visuals))
        {
            return;
        }

        // 视觉被重建（换皮肤 / 形态切换）后缓存会失效：清掉退回兜底身高，不要每帧重查。
        if (_bounds != null && !GodotObject.IsInstanceValid(_bounds))
        {
            _bounds = null;
        }

        float height = _bounds?.Size.Y ?? DefaultBodyHeight;
        Vector2 target = owner.VfxSpawnPosition
                         + new Vector2(0f, -(height * BodyTopRatio + HeadClearance) + bob);
        float k = 1f - Mathf.Exp(-FollowRate * Mathf.Max(0.0001f, delta));
        root.GlobalPosition = root.GlobalPosition.Lerp(target, k);
    }

    private static bool IsAlive(GodotObject? obj) => obj != null && GodotObject.IsInstanceValid(obj);
}
