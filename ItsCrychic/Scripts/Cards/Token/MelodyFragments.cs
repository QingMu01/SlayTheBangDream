using BangDreamLib.Scripts.Cards;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Mechanics.MusicNote;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using ItsCrychic.Scripts.Character.CardPools;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ItsCrychic.Scripts.Cards.Token;

[RegisterCard(typeof(TokenCardPool))]
public class MelodyFragments() : MusicCardModel(CustomCost, CustomRarity, CustomTarget)
{
    private const int CustomCost = 0;
    private const CardRarity CustomRarity = CardRarity.Token;
    private const TargetType CustomTarget = TargetType.None;

    public override bool IsInstant => true;

    public override CardPoolModel VisualCardPool => ModelDb.CardPool<SakikoMusicalCardPool>();

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust,
        BangDreamConst.MusicNote
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Repeat.Create(4),
        QuickVar.LingeredResource.Create(2)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        await MusicNoteCmd.FromCard(this, DynamicVars.Repeat.IntValue);
        await SecondaryResourceCmd.Gain(Owner, BangDreamConst.LingeredResource,
            QuickVar.LingeredResource.GetVar(this).IntValue, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(2);
    }
}