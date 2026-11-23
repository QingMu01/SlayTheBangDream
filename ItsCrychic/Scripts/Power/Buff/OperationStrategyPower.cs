using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Interfaces.GameHook;
using BangDreamInSpire.Scripts.Powers;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Power.Buff;

/// <summary>回合开始复制上回合最后演奏的音乐牌，保留原目标类型。</summary>
public class OperationStrategyPower : BandPowerModel, IPerformHookListener
{
    private CardModel? _lastPerformedMusicCard;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public Task OnCardPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        if (perform.Player == Owner.Player && perform.Card is IPerformCard)
        {
            _lastPerformedMusicCard = perform.Card;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player) return;

        var performedCard = _lastPerformedMusicCard;
        _lastPerformedMusicCard = null;

        if (performedCard == null) return;

        Flash();
        await CardPileCmd.Add(performedCard.CreateClone(), PileType.Hand);
    }
}
