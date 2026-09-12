using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class GrittedTeeth() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private const TargetType CustomTarget = TargetType.Self;

    private bool _returnNextTurn;

    public override bool GainsBlock => true;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Lingered
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Block.Create(5),
        QuickVar.LingeredResource.Create(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        await SecondaryResourceCmd.Lose(Owner, BangDreamConst.LingeredResource,
            QuickVar.LingeredResource.GetVar(this).IntValue, this);
    }

    public override Task AfterCardPlayedLate(PlayerChoiceContext choiceContext, CardPlay play)
    {
        if (play.Card == this)
        {
            _returnNextTurn = true;
        }

        return Task.CompletedTask;
    }

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        if (player != Owner || !_returnNextTurn) return;

        _returnNextTurn = false;
        if (Pile is { Type: PileType.Hand }) return;

        await CardPileCmd.Add(this, PileType.Hand);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        QuickVar.LingeredResource.GetVar(this).UpgradeValueBy(-1m);
    }
}
