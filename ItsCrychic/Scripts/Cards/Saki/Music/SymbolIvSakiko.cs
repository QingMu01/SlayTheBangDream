using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class SymbolIvSakiko() : AbstractSakikoMusicCard(CustomRarity, CustomTarget), IPerformSealCard
{
    /// <summary>演奏反馈用哪个元素印；与卡图主题一致（见 docs/mechanic-authoring.md 演奏特效章节）。</summary>
    public PerformSealElement PerformSeal => PerformSealElement.Earth;

    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private static readonly TargetType CustomTarget = PerformTargetTypes.RequestGroup;

    public override bool IsInstant => true;

    protected override HashSet<CardTag> CanonicalTags =>
    [
        BangDreamConst.SymbolCard
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Energy.Create(1)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Energy.UpgradeValueBy(1m);
    }
}