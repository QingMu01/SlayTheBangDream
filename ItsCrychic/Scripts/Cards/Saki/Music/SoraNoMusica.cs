using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class SoraNoMusica() : AbstractSakikoMusicCard(CardRarity.Rare, TargetType.None), IPerformAreaHook
{
    public override bool IsInstant { get; set; } = true;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.Instant,
        BangDreamConst.Perform
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        ModCardVars.Int("IncreaseStep", 1),
        ModCardVars.Int("IncreaseCount", 0),
        ComputedDynamicVarHelper.CreateBaseVar("Notes", 5m, card =>
        {
            if (card != null && card.DynamicVars.TryGetValue("IncreaseStep", out var step))
            {
                if (card.DynamicVars.TryGetValue("IncreaseCount", out var count))
                {
                    return step.IntValue * count.IntValue + 5m;
                }
            }

            return 5m;
        })
    ];

    public override async Task OnStartPerform(PlayerChoiceContext choiceContext)
    {
        await MusicNoteCmd.FromCard(this, (int)DynamicVars.ComputedValue("Notes"));
    }

    public Task OnCardEnterPerformArea(CardModel cardModel)
    {
        if (cardModel is IPerformCard)
        {
            DynamicVars["IncreaseCount"].BaseValue += 1;
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["IncreaseStep"].UpgradeValueBy(1);
    }
}