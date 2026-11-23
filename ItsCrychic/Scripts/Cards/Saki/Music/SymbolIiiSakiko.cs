using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class SymbolIiiSakiko() : AbstractSakikoMusicCard(CustomRarity, CustomTarget), IPerformSlotVfxCard
{
    /// <summary>演奏反馈用哪套墨色；内置元素之印见 docs/mechanic-authoring.md 12.1。</summary>
    public string? PerformSlotVfxKey => PerformSlotVfxKeys.SymbolWater;

    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private static readonly TargetType CustomTarget = PerformTargetTypes.RequestGroup;

    public override bool IsInstant => true;

    public override bool GainsBlock => true;

    protected override HashSet<CardTag> CanonicalTags =>
    [
        BangDreamConst.SymbolCard
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Block.Create(10)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, null);
    }


    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(5m);
    }
}