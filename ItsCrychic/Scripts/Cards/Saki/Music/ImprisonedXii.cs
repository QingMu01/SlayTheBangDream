using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class ImprisonedXii() : AbstractSakikoMusicCard(CardRarity.Rare, TargetType.None)
{
    protected override IEnumerable<CardKeyword> CardKeywords => [BangDreamConst.Perform];

    protected override IEnumerable<DynamicVar> CardVars => [ModCardVars.Int("MinBlock", 4)];

    public override async Task OnStartPerform(PlayerChoiceContext choiceContext)
    {
        await RefillBlock();
    }

    public override async Task AfterBlockBroken(Creature creature)
    {
        await RefillBlock();
    }

    public override async Task AfterBlockCleared(Creature creature)
    {
        await RefillBlock();
    }

    protected override void OnUpgrade()
    {
        DynamicVars["MinBlock"].UpgradeValueBy(2);
    }

    private async Task RefillBlock()
    {
        var target = Owner.Creature;
        var amount = DynamicVars["MinBlock"].IntValue - target.Block;
        if (amount > 0)
        {
            await CreatureCmd.GainBlock(target, new BlockVar(amount, ValueProp.Unpowered), null);
        }
    }
}