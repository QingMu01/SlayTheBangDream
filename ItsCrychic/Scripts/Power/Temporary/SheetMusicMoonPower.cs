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

    /// <summary>
    /// 由卡牌在施加 / 叠层后登记。能力到期或被移除时，需要反过来确认这张牌是否还在歌单里
    /// （H-09 双向校验）。
    /// </summary>
    internal void LinkCard(CardModel card) => _linkedCards.Add(card);

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        // 先通知卡牌：能力即将被移除，牌已不在歌单时按承诺转永久；牌仍在歌单时本回合内有效、到期失效。
        // 通知后清空登记，随后 base 会移除能力并扣回临时敏捷（净效果 = 转永久的量被保留）。
        if (participants.Contains(Owner))
        {
            await NotifyLinkedCardsAsync(choiceContext);
        }

        await base.AfterSideTurnEnd(choiceContext, side, participants);
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        // 非回合结束的移除（驱散 / 死亡 / 战斗结束）也要做一次双向校验：
        // 牌已不在歌单 → 转永久；牌仍在歌单 → 失效。战斗结束时 IsInProgress=false，不会补属性。
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
