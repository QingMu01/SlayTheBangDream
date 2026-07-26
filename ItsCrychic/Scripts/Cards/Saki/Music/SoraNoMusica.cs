using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class SoraNoMusica() : AbstractSakikoMusicCard(CardRarity.Rare, TargetType.None), IPerformHookListener
{
    private const int BaseNoteCount = 8;
    private const int BaseNoteDamage = 1;

    public override bool IsInstant => true;

    protected override IEnumerable<CardKeyword> CardKeywords => [];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Repeat.Create(BaseNoteCount),
        QuickVar.Damage.Create(BaseNoteDamage)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        var noteDamage = DynamicVars.Damage.BaseValue;
        DynamicVars.Damage.BaseValue = BaseNoteDamage;
        await MusicNoteCmd.FromCard(this, DynamicVars.Repeat.IntValue, baseDamage: noteDamage);
    }

    public Task OnCardPerform(PlayerChoiceContext choiceContext, PerformContext ctx, CardModel cardModel)
    {
        if (cardModel.Owner == Owner)
        {
            DynamicVars.Damage.BaseValue += 1m;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        DynamicVars.Repeat.BaseValue = IsUpgraded ? BaseNoteCount + 2 : BaseNoteCount;
        DynamicVars.Damage.BaseValue = BaseNoteDamage;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(2);
    }
}
