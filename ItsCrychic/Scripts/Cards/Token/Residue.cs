using BangDreamLib.Scripts.Cards;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ItsCrychic.Scripts.Cards.Token;

[RegisterCard(typeof(TokenCardPool))]
public class Residue() : BandCardModel(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Status;
    private const CardRarity CustomRarity = CardRarity.Status;
    private const TargetType CustomTarget = TargetType.None;
    private bool _isRemoving;

    protected override CardAssetProfile CardAssetProfile => CrychicConst.DefaultCardAssetProfile(this);

    protected override IEnumerable<CardKeyword> CardKeywords => [];

    protected override IEnumerable<DynamicVar> CardVars => [];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        return Task.CompletedTask;
    }

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? source)
    {
        if (card == this && oldPileType == BangDreamConst.PerformPile && Pile?.Type != BangDreamConst.PerformPile)
        {
            await RemoveFromCombat();
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && participants.Contains(Owner.Creature) &&
            Pile?.Type == BangDreamConst.PerformPile)
        {
            await RemoveFromCombat();
        }
    }

    private async Task RemoveFromCombat()
    {
        if (_isRemoving) return;

        _isRemoving = true;
        try
        {
            await CardPileCmd.RemoveFromCombat(this);
        }
        finally
        {
            _isRemoving = false;
        }
    }
}
