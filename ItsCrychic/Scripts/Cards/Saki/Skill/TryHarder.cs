using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using ItsCrychic.Scripts.Cards.Token;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ItsCrychic.Scripts.Cards.Saki.Skill;

public class TryHarder() : AbstractSakikoCard(CustomCost, CustomType, CustomRarity, CustomTarget)
{
    private const int CustomCost = 1;
    private const CardType CustomType = CardType.Skill;
    private const CardRarity CustomRarity = CardRarity.Rare;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<CardKeyword> CardKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromCard<GiantNote>(IsUpgraded)
    ];

    protected override IEnumerable<DynamicVar> CardVars =>
    [
        QuickVar.Buff.Create(1)
    ];


    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        ArgumentNullException.ThrowIfNull(Owner.PlayerCombatState);

        var manager = Owner.AttachedData().PerformManager;
        manager.AddCapacity(QuickVar.Buff.GetVar(this).IntValue);

        var musicCards = Owner.PlayerCombatState.AllCards
            .Where(card => card is IPerformCard)
            .ToList();

        foreach (var card in musicCards)
        {
            var giantNote = CombatState.CreateCard<GiantNote>(Owner);
            if (IsUpgraded) CardCmd.Upgrade(giantNote);
            if (manager.PerformPile.Cards.Contains(card))
            {
                var originalContext = manager.CardContexts.GetOrCreate(card);
                var replacementContext = manager.CardContexts.GetOrCreate(giantNote);
                replacementContext.Manager = manager;
                replacementContext.SlotIndex = originalContext.SlotIndex;
                var result = await CardCmd.Transform(card, giantNote);
                if (result is not { success: true })
                    manager.CardContexts.Remove(giantNote);
            }
            else
            {
                await CardCmd.Transform(card, giantNote);
            }
        }
    }

    protected override void OnUpgrade()
    {
        QuickVar.Buff.GetVar(this).UpgradeValueBy(1);
    }
}