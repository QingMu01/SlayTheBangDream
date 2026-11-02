using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Mechanics.MusicNote;
using BangDreamInSpire.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class RisingTone() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget),
    ISubsideCard
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.AllEnemies;

    public int LingeredResourceCost => IsUpgraded ? 1 : 2;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.MusicNote
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Repeat.Create(3),
        ModCardVars.Int("Count", 0),
        ComputedDynamicVarHelper.CreateBaseVar("CalcNote", 4m, ctx =>
        {
            if (ctx.IsInCombat() && ctx.ActiveCard.DynamicVars.TryGetValue(RepeatVar.defaultName, out var repeat) &&
                ctx.ActiveCard.DynamicVars.TryGetValue("Count", out var count))
            {
                return ctx.BaseValue + repeat.IntValue * count.IntValue;
            }

            return ctx.BaseValue;
        })
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await MusicNoteCmd.FromCard(this, (int)DynamicVars.ComputedValue("CalcNote"));
        EnergyCost.AddThisCombat(1);
        DynamicVars["Count"].BaseValue++;
    }

    public Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        EnergyCost.AddThisCombat(-1, true);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        // 升级后休止所需余音：2 → 1。
        this.SecondaryCosts().Set(BangDreamConst.LingeredResource, 1);
    }
}