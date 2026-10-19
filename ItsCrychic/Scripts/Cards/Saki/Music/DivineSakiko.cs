using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Mechanics.MusicNote;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class DivineSakiko() : AbstractSakikoMusicCard(CustomRarity, CustomTarget)
{
    private const CardRarity CustomRarity = CardRarity.Uncommon;
    private static readonly TargetType CustomTarget = PerformTargetTypes.AnySlot;

    protected override IEnumerable<CardKeyword> CardKeywords => [BangDreamConst.MusicNote];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Repeat.Create(4),
        QuickVar.Repeat.Create("SlotMultiplier", 2)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        var slot = Owner.AttachedData().PerformManager.CardContexts.GetOrCreate(this).SlotIndex;
        await MusicNoteCmd.FromCard(this, DynamicVars.Repeat.IntValue + Math.Max(0, slot) *
            DynamicVars["SlotMultiplier"].IntValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["SlotMultiplier"].UpgradeValueBy(1);
    }
}