using BangDreamLib.Scripts.Commands;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class CrescentAwakening() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        new RepeatVar(5)
    ];


    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await MusicNoteCmd.FromCard(choiceContext, this, DynamicVars.Repeat.IntValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(3);
    }
}