using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Mechanics.MusicNote;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class RisingTone() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget),
    ISubsideCard
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.AllEnemies;

    public int LingeredResourceCost => 2;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.MusicNote
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Repeat.Create(2),
        ComputedDynamicVarHelper.CreateBaseVar("CalcNote", 4m, ctx =>
        {
            if (ctx.IsInCombat() && ctx.ActiveCard.DynamicVars.TryGetValue(RepeatVar.defaultName, out var repeat))
            {
                var count = CombatManager.Instance.History.CardPlaysFinished.Count(entry => entry.CardPlay.Card == this);
                return ctx.BaseValue + repeat.IntValue * count;
            }

            return ctx.BaseValue;
        })
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await MusicNoteCmd.FromCard(this, (int)DynamicVars.ComputedValue("CalcNote"));
        EnergyCost.AddThisCombat(1);
    }

    public Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        EnergyCost.AddThisCombat(-1, true);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1m);
    }
}