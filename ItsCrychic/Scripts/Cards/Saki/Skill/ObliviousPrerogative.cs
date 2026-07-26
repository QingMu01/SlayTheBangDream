using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class ObliviousPrerogative()
    : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCard
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.None;

    public int LingeredResourceCost => 5;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Repeat.Create(2),
    ];

    private CardModel? _autoPlayCard;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        var selectedCard = (await CardSelectCmd.FromCombatPile(choiceContext,
            PileType.Hand.GetPile(Owner),
            Owner,
            CardSelectorPrompt.ToPlay.GetFixedPrefs(1)
        )).FirstOrDefault();

        if (play.SecondaryResources().HasLines &&
            play.SecondaryResources().Shortfall(BangDreamConst.LingeredResource) == 0)
        {
            _autoPlayCard = selectedCard;
        }

        if (selectedCard != null)
        {
            await CardCmd.AutoPlay(choiceContext, selectedCard, null);
        }

        _autoPlayCard = null;
    }

    public Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        return Task.CompletedTask;
    }

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        return _autoPlayCard == card ? playCount + DynamicVars.Repeat.IntValue - 1 : playCount;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1);
    }
}