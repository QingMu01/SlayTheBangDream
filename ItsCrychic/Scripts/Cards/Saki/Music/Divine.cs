using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class Divine() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None)
{
    protected override IEnumerable<CardKeyword> CardKeywords => [BangDreamConst.MusicNote];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Repeat.Create(4),
        QuickVar.Repeat.Create("SlotMultiplier", 2)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        var slot = Owner.AttachedData().PerformManager.CardContexts.GetOrCreate(this).SlotIndex;
        await MusicNoteCmd.FromCard(this, DynamicVars.Repeat.IntValue + Math.Max(0, slot) *
            DynamicVars["SlotMultiplier"].IntValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(3);
    }
}