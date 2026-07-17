using BangDreamLib.Scripts.Commands;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.GameHook;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class CrescentAwakening() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget), IPerformHookListener
{
    private const int CustomCost = 0;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Common;
    private const TargetType CustomTarget = TargetType.None;
    private int _performCount;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        BangDreamConst.MusicNote
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Repeat.Create(4),
        QuickVar.Cards.Create(3)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        await MusicNoteCmd.FromCard(this, DynamicVars.Repeat.IntValue);
    }

    public async Task OnCardPerform(PlayerChoiceContext choiceContext, PerformContext ctx, CardModel cardModel)
    {
        if (cardModel.Owner != Owner || CombatState == null) return;

        _performCount++;
        if (_performCount >= DynamicVars.Cards.IntValue)
        {
            _performCount = 0;
            if (Pile?.Type != PileType.Hand)
            {
                await CardPileCmd.Add(this, PileType.Hand);
            }
        }
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        _performCount = 0;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(2);
    }
}
