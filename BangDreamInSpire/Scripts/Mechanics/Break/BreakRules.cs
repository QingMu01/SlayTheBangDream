using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace BangDreamInSpire.Scripts.Mechanics.Break;

/// <summary>
/// 击破触发规则（与祥子「余音 / 休止」同构的单例模型）。
/// <para>
/// 每张牌出牌期间维护一张「命中集合」：<see cref="BeforeCardPlayed" /> 压栈并先记入
/// <see cref="CardPlay.Target" />（覆盖纯指向牌），出牌结算过程中通过
/// <see cref="AfterDamageReceived" /> / <see cref="AfterPowerAmountChanged" /> 按
/// <c>cardSource</c> 归属把被命中的目标补进来（覆盖随机 / AOE / 非伤害效果）。
/// 随后在 <see cref="AfterCardPlayedLate" />（此时「发现破绽 → 弱点击破」已经结算完）出栈，
/// 对集合里每一个处于弱点击破的目标触发 <see cref="IWeaknessBreakCard.OnWeakPointBreak" /> 并广播。
/// </para>
/// <para>
/// 嵌套出牌用帧栈区分：内层出牌推自己的帧、用 <see cref="CardPlay" /> 精确出栈，
/// 因此外层牌的效果触发内层出牌时，两边各结算各自的命中集合。
/// </para>
/// </summary>
[RegisterSingleton]
public sealed class BreakRules : HookedSingletonModel
{
    /// <summary>命中集合帧栈的硬上限：出牌被异常中断（拥有者死亡、战斗结束）时防止无限累积。</summary>
    private const int FrameStackHardLimit = 64;

    private List<CardHitFrame> _frames = [];

    public BreakRules() : base(HookType.Combat)
    {
    }

    /// <summary>一次出牌对应的命中集合。</summary>
    private sealed class CardHitFrame(CardModel card, CardPlay play)
    {
        public CardModel Card { get; } = card;

        public CardPlay Play { get; } = play;

        public HashSet<Creature> Targets { get; } = [];

        public bool Triggered { get; set; }
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        // 帧栈是纯运行时状态：每局实例各持一份，不随规范模型浅拷贝共享。
        _frames = [];
    }

    /// <summary>战斗开始时清空上一场可能残留的帧（出牌被中断时不会走到出栈）。</summary>
    public override Task BeforeCombatStart()
    {
        _frames.Clear();
        return Task.CompletedTask;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        _frames.Add(new CardHitFrame(cardPlay.Card, cardPlay));
        if (_frames.Count > FrameStackHardLimit)
        {
            _frames.RemoveAt(0);
        }

        // 纯指向牌：目标一定能被记录，即便这张牌没有伤害、也没有施加能力。
        RecordTarget(cardPlay.Card, cardPlay.Target);
        return Task.CompletedTask;
    }

    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // 随机 / AOE 伤害：每个被命中的目标各自回调一次，按 cardSource 归属到当前出牌。
        RecordTarget(cardSource, target);
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount,
        Creature? applier, CardModel? cardSource)
    {
        // 非伤害类命中（施加易伤/虚弱、发现破绽等）：能力目标即被命中目标。
        // 排除「这张牌给牌主自己上的增益」——那是自身效果、不是「目标被选中」；
        // 显式指向自己时由 BeforeCardPlayed 里的 CardPlay.Target 记录，不会漏。
        if (power.Owner is { } owner && !ReferenceEquals(owner, cardSource?.Owner?.Creature))
        {
            RecordTarget(cardSource, owner);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var frame = PopFrame(cardPlay);
        if (frame == null || frame.Triggered)
        {
            return;
        }

        frame.Triggered = true;
        if (cardPlay.Card is not IWeaknessBreakCard { HasBreakEffect: true } breakCard)
        {
            return;
        }

        // 逐个目标触发：AOE 命中多个弱点击破目标时各自结算一次；同一张牌对同一目标只触发一次。
        foreach (var target in frame.Targets)
        {
            if (target.IsDead || target.GetPower<WeakPointBreakPower>() == null)
            {
                continue;
            }

            await breakCard.OnWeakPointBreak(choiceContext, cardPlay, target);

            if (target.CombatState is { } combatState)
            {
                await BangDreamHook.AfterWeakPointBreakTriggered(choiceContext, combatState, target, cardPlay);
            }
        }
    }

    private void RecordTarget(CardModel? cardSource, Creature? target)
    {
        if (cardSource == null || target == null || _frames.Count == 0)
        {
            return;
        }

        // 从栈顶向下找「这张牌」的帧：内层出牌命中记到内层帧，外层记到外层帧。
        for (var i = _frames.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(_frames[i].Card, cardSource))
            {
                continue;
            }

            _frames[i].Targets.Add(target);
            return;
        }
    }

    private CardHitFrame? PopFrame(CardPlay cardPlay)
    {
        for (var i = _frames.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(_frames[i].Play, cardPlay))
            {
                continue;
            }

            var frame = _frames[i];
            _frames.RemoveAt(i);
            return frame;
        }

        return null;
    }
}
