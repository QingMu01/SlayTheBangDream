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

/// <summary>按出牌帧栈收集命中目标，AfterCardPlayedLate 对仍处于弱点击破的目标触发击破；
/// 嵌套出牌独立结算。</summary>
[RegisterSingleton]
public sealed class BreakRules : HookedSingletonModel
{
    /// <summary>帧栈上限，防止异常中断导致累积。</summary>
    private const int FrameStackHardLimit = 64;

    private List<CardHitFrame> _frames = [];

    public BreakRules() : base(HookType.Combat)
    {
    }

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
        // 帧栈不随规范模型浅拷贝共享。
        _frames = [];
    }

    /// <summary>战斗开始时清空残留帧。</summary>
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

        // 纯指向牌始终记录目标。
        RecordTarget(cardPlay.Card, cardPlay.Target);
        return Task.CompletedTask;
    }

    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // 按 cardSource 归属伤害目标。
        RecordTarget(cardSource, target);
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount,
        Creature? applier, CardModel? cardSource)
    {
        // 能力目标也计入命中；自身增益不算，除非显式指向自己。
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

        // 只要求目标当前处于弱点击破，不要求本牌造成击破；每个目标至多触发一次。
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

        // 从栈顶匹配当前出牌帧，区分嵌套结算。
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
