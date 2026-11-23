using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class SymbolISakiko() : AbstractSakikoMusicCard(CustomRarity, CustomTarget), IPerformSealCard
{
    /// <summary>演奏反馈用哪个元素印；与卡图主题一致（见 docs/mechanic-authoring.md 演奏特效章节）。</summary>
    public PerformSealElement PerformSeal => PerformSealElement.Fire;

    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private static readonly TargetType CustomTarget = PerformTargetTypes.RequestGroup;

    public override bool IsInstant => true;

    protected override HashSet<CardTag> CanonicalTags =>
    [
        BangDreamConst.SymbolCard
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<VigorPower>()
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Buff.Create(5)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, QuickVar.Buff.GetVar(this).BaseValue,
            Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        QuickVar.Buff.GetVar(this).UpgradeValueBy(2m);
    }
}