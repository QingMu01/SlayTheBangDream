using BangDreamLib.Scripts.Features.Rule;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;

namespace BangDreamLib.Scripts.Capability;

[RegisterModelCapability]
public class SubsideCapability : CardCapability, ICardDescriptionContributor, ICardHoverTipContributor,
    ICardGlowContributor
{
    private const string SubsidePostfix = ".subside";

    private static readonly LocString Text = new("gameplay_ui", "BANG_DREAM_LIB_SUBSIDE_PLAY_TEXT");

    private static readonly LocString Title = new("static_hover_tips", "BANG_DREAM_LIB_SUBSIDE.title");
    private static readonly LocString Description = new("static_hover_tips", "BANG_DREAM_LIB_SUBSIDE.description");

    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context)
    {
        var cardDescription = new LocString("cards", context.Card.Id.Entry + SubsidePostfix);
        context.Card.DynamicVars.AddTo(cardDescription);
        Text.Add(new StringVar("desc", cardDescription.GetFormattedText()));
        yield return new CardDescriptionFragment(Text);
    }

    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        card.TryGetSecondaryCosts(out var set);
        if (set.ResourceIds.Contains(BangDreamConst.LingeredResource))
        {
            Description.Add("Amount", SecondaryResourcePaymentResolver.Plan(card).Lines
                .Where(line => line.ResourceId.Equals(BangDreamConst.LingeredResource))
                .Sum(line => line.Value));
            yield return new HoverTip(Title, Description);
        }
    }

    public bool ShouldGlowGold(CardModel card)
    {
        return LingeredResourcesRule.IsSufficient(card);
    }
}