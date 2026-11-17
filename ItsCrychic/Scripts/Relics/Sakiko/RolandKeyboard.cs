using BangDreamInSpire.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Relics.Sakiko;

public class RolandKeyboard : AbstractSakikoRelic, ISecondaryResourceHookListener
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    private bool _canEffective = true;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        _canEffective = true;
        return Task.CompletedTask;
    }

    public decimal ModifySecondaryResourceCost(SecondaryResourceCostContext context, decimal cost)
    {
        return _canEffective ? 0m : cost;
    }

    public override Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay play)
    {
        // 与 Synthesizer 保持一致：X 费余音牌不消耗「首次免费休止」。
        if (!play.Card.IsDupe && _canEffective && play.Card.SecondaryCosts().HasCosts &&
            !play.SecondaryResources().CostsX(BangDreamConst.LingeredResource))
        {
            Flash();
            _canEffective = false;
        }

        return Task.CompletedTask;
    }
}