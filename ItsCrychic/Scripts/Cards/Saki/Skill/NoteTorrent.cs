using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using ItsCrychic.Scripts.Character;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

[RegisterDustyTomeCard(typeof(TogawaSakiko))]
public class NoteTorrent() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), ISubsideCard
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Ancient;
    private const TargetType CustomTarget = TargetType.AllEnemies;

    protected override bool HasEnergyCostX => true;

    public int LingeredResourceCost => -1;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.MusicNote,
        BangDreamConst.Lingered
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ModCardVars.Int("NotesPerEnergy", 4),
        ModCardVars.Int("ExtraNotesPerEnergy", 2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);

        await PlayNotesToAllEnemies(DynamicVars["NotesPerEnergy"].IntValue * ResolveEnergyXValue());
    }

    public async Task OnSubside(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await PlayNotesToAllEnemies(DynamicVars["ExtraNotesPerEnergy"].IntValue *
                                    play.SecondaryResources().Value(BangDreamConst.LingeredResource));
    }

    protected override void OnUpgrade()
    {
        DynamicVars["NotesPerEnergy"].UpgradeValueBy(1);
        DynamicVars["ExtraNotesPerEnergy"].UpgradeValueBy(1);
    }

    private async Task PlayNotesToAllEnemies(int count)
    {
        if (count <= 0 || CombatState == null) return;

        foreach (var enemy in CombatState.HittableEnemies.ToList())
        {
            await MusicNoteCmd.FromCard(this, count, target: enemy);
        }
    }
}