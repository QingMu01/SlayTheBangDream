using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class SophieSakiko() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
{
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private static readonly TargetType CustomTarget = PerformTargetTypes.RequestGroup;

    // 升级的即兴在入场时也结算。
    public override bool IsInstant => IsUpgraded;

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Cards.Create(1)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}
