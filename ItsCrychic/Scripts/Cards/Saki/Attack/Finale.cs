using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Keywords;

namespace ItsCrychic.Scripts.Cards.Saki.Attack;

public class Finale() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 3;
    private const CardType CustomType = CardType.Attack;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.AllEnemies;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.KeywordMusicNote.GetModCardKeyword()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ModCardVars.Int("IncreaseStep", 2),
        ModCardVars.Computed("CalcDamage", 8m, card =>
                DynamicVarHelper.ResolveBaseVar(card, CalculateDamage),
            (card, mode, target, runHooks) =>
                DynamicVarHelper.ResolvePreviewDamageVar(card, mode, target, runHooks, CalculateDamage))
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        await DamageCmd.Attack(DynamicVars.ComputeVar("CalcDamage").Calculate())
            .FromCard(this)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    private static decimal CalculateDamage(CardModel? card)
    {
        if (card != null)
        {
            var intValue = card.DynamicVars["IncreaseStep"].IntValue;
            var musicNoteCount = card.Owner.AttachedData().MusicNoteDamageTracker.GetAllDamageResults().Count;
            return 8m + intValue * musicNoteCount;
        }

        return 8m;
    }
}