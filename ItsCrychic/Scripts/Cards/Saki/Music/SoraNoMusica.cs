using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Mechanics.MusicNote;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Cards.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Music;

public class SoraNoMusica() : AbstractSakikoMusicCard(CardRarity.Rare, TargetType.None), IPerformHookListener
{
    private const int BaseNoteCount = 8;
    public override bool IsInstant => true;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.MusicNote
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Repeat.Create(BaseNoteCount),
        ModCardVars.Int("AdditiveDamage", 0m),
        ComputedDynamicVarHelper.CreateBaseVar("CalcDamage", 1m, ctx =>
        {
            if (ctx.IsInCombat() && ctx.ActiveCard.DynamicVars.TryGetValue("AdditiveDamage", out var baseDamage))
            {
                return ctx.BaseValue + baseDamage.IntValue;
            }

            return ctx.BaseValue;
        })
    ];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        await MusicNoteCmd.FromCard(this, DynamicVars.Repeat.IntValue, 0, DynamicVars.ComputedValue("CalcDamage"));
        if (DynamicVars.TryGetValue("AdditiveDamage", out var additiveDamage))
        {
            additiveDamage.BaseValue = 0;
        }
    }

    public Task OnCardPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        if (perform.Card.Owner == Owner && DynamicVars.TryGetValue("AdditiveDamage", out var additiveDamage))
        {
            additiveDamage.BaseValue++;
        }

        return Task.CompletedTask;
    }


    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(2);
    }
}