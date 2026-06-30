using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Cards.Token;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class AttendantServant() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.Self;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Ethereal
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromCard<SakikoShield>()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Cards.Create(2),
        QuickVar.Cards.Create("Draw", 1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        var selectedCards = await CardSelectCmd.FromCombatPile(choiceContext,
            BangDreamConst.ExtraDraw.GetPile(Owner),
            Owner,
            CardSelectorPrompt.ToTransform.GetFixedPrefs(DynamicVars.Cards.IntValue)
        );
        foreach (var selectedCard in selectedCards)
        {
            await CardCmd.TransformTo<SakikoShield>(selectedCard);
        }

        await ExtraPileCmd.Draw(choiceContext, DynamicVars["Draw"].IntValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Draw"].UpgradeValueBy(1m);
    }
}