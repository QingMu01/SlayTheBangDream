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
    public override bool IsInstant => true;

    protected override IEnumerable<CardKeyword> CardKeywords => [];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Repeat.Create(5),
        QuickVar.Repeat.Create("Increase", 1)
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext)
    {
        await MusicNoteCmd.FromCard(this, DynamicVars.Repeat.IntValue);
    }

    public Task OnCardPerform(PlayerChoiceContext choiceContext, PerformContext ctx, CardModel cardModel)
    {
        if (cardModel.Owner == Owner)
        {
            DynamicVars.Repeat.BaseValue += DynamicVars["Increase"].IntValue;
        }
        return Task.CompletedTask;
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        DynamicVars.Repeat.BaseValue = 5;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Increase"].UpgradeValueBy(1);
    }
}
