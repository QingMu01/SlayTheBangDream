using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class InYourBlueEyes() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    public override bool IsInstant => true;

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<BlurPower>()
    ];

    protected override IEnumerable<DynamicVar> CardVars => [];

    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        await PowerCmd.Apply<BlurPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (IsUpgraded && player == Owner && Pile?.Type == BangDreamConst.PerformPile)
        {
            await CardPileCmd.Add(this, BangDreamConst.ExtraDraw, CardPilePosition.Bottom);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
