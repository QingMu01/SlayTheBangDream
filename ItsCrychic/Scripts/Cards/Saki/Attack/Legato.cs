using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Legato() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.AnyEnemy;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Lingered
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Damage.Create(4),
        ModCardVars.Int("MusicNote", 1),
        ComputedDynamicVarHelper.CreateBaseVar("RepeatCount", 0m,
            ctx => ctx.IsInCombat()
                ? BangDreamConst.PerformPile.GetPile(ctx.ActiveCard.Owner).Cards.Count
                : ctx.BaseValue)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var cardsCount = BangDreamConst.PerformPile.GetPile(Owner).Cards.Count;
        if (cardsCount > 0)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, play)
                .Targeting(play.Target)
                .WithHitCount(cardsCount)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
            await MusicNoteCmd.FromCard(this, DynamicVars["MusicNote"].IntValue * cardsCount);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["MusicNote"].UpgradeValueBy(1m);
    }
}