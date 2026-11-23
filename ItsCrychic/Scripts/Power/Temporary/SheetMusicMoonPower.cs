using ItsCrychic.Scripts.Cards.Saki.Music;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ItsCrychic.Scripts.Power.Temporary;

public class SheetMusicMoonPower : TemporaryDexterityPower
{
    private readonly HashSet<CardModel> _linkedCards = [];

    public override AbstractModel OriginModel => ModelDb.Card<SheetMusicMoon>();

    /// <summary>登记能力移除时需要复核歌单状态的卡牌。</summary>
    internal void LinkCard(CardModel card) => _linkedCards.Add(card);

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        // 在 base 扣回临时能力前通知卡牌，避免永久转化被抵消。
        if (participants.Contains(Owner))
        {
            await NotifyLinkedCardsAsync(choiceContext);
        }

        await base.AfterSideTurnEnd(choiceContext, side, participants);
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        // 驱散、死亡等非到期移除也要复核卡牌；战斗结束时不补属性。
        await NotifyLinkedCardsAsync(new BlockingPlayerChoiceContext());
    }

    private async Task NotifyLinkedCardsAsync(PlayerChoiceContext choiceContext)
    {
        if (_linkedCards.Count == 0) return;

        foreach (var card in _linkedCards.ToArray())
        {
            if (card is SheetMusicMoon moon)
            {
                await moon.OnTemporaryPowerExpiringAsync(choiceContext);
            }
        }

        _linkedCards.Clear();
    }
}
