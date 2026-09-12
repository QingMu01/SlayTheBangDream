using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class SheetMusicSakiko() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
{
    private const CardRarity CustomRarity = CardRarity.Basic;
    private const TargetType CustomTarget = TargetType.None;

    public override bool IsInstant => true;

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>()
    ];

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.PerformArea
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Buff.Create(1)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        var amount = QuickVar.Buff.GetVar(this).BaseValue;
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, amount,
            Owner.Creature, this);
        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, amount,
            Owner.Creature, this);

        if (!perform.IsAutoPerform)
        {
            await CardCmd.Exhaust(choiceContext, this);
        }
    }

    protected override void OnUpgrade()
    {
        QuickVar.Buff.GetVar(this).UpgradeValueBy(1);
    }
}