using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class Face() : AbstractSakikoMusicCard(CardRarity.Uncommon, TargetType.None), ISecondaryResourceHookListener
{
    private decimal _lastLingered;

    protected override IEnumerable<CardKeyword> CardKeywords => [BangDreamConst.Perform];

    protected override IEnumerable<DynamicVar> CardVars => [ModCardVars.Int("Notes", 1)];

    public override Task OnStartPerform(PlayerChoiceContext choiceContext)
    {
        _lastLingered = SecondaryResourceCmd.Get(Owner, BangDreamConst.LingeredResource);
        return Task.CompletedTask;
    }

    public async Task AfterSecondaryResourceChanged(SecondaryResourceChangeContext context)
    {
        if (Handle == null || !context.Definition.Id.Equals(BangDreamConst.LingeredResource) ||
            context.Player != Owner) return;


        if (context.NewAmount != _lastLingered)
        {
            _lastLingered = context.NewAmount;
            await MusicNoteCmd.FromCard(this, DynamicVars["Notes"].IntValue);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Notes"].UpgradeValueBy(1);
    }
}