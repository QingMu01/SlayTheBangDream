using BangDreamLib.Scripts.Cards;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Mechanics.MusicNote;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ItsCrychic.Scripts.Cards.Token;

[RegisterCard(typeof(TokenCardPool))]
public sealed class BasicScale() : MusicCardModel(CustomCost, CustomRarity, CustomTarget)
{
    private const int CustomCost = 0;
    private const CardRarity CustomRarity = CardRarity.Token;
    private const TargetType CustomTarget = TargetType.None;

    protected override IEnumerable<DynamicVar> CardVars => [QuickVar.Repeat.Create(3)];

    public override async Task OnPerform(PlayerChoiceContext choiceContext, CardPerform perform)
    {
        await MusicNoteCmd.FromCard(this, DynamicVars.Repeat.IntValue);

        var manager = Owner.AttachedData().PerformManager;
        var topCard = BangDreamConst.PerformPile.GetPile(Owner).Cards
            .OrderByDescending(card => manager.CardContexts.GetOrCreate(card).SlotIndex)
            .FirstOrDefault();
        if (topCard == this)
        {
            await SecondaryResourceCmd.Reset(Owner, BangDreamConst.LingeredResource);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1);
    }
}