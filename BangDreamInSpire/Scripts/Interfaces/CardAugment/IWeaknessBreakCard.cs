using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace BangDreamInSpire.Scripts.Interfaces.CardAugment;

/// <summary>击破卡契约；命中弱点击破目标时，每个目标触发一次。</summary>
public interface IWeaknessBreakCard
{
    /// <summary>是否具有击破效果。</summary>
    bool HasBreakEffect { get; }

    /// <summary>在 AfterCardPlayedLate 对弱点击破目标执行击破效果。</summary>
    Task OnWeakPointBreak(PlayerChoiceContext choiceContext, CardPlay play, Creature target);
}
