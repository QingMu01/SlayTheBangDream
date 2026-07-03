using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Models.Capabilities;

namespace BangDreamLib.Scripts.Capability;

public class SubsideCapability : CardCapability, ICardDescriptionContributor, ICardHoverTipContributor
{
    private const string SubsidePostfix = ".subside";

    private static readonly LocString Title = new("static_hover_tips", "BANG_DREAM_LIB_SUBSIDE.title");
    private static readonly LocString Description = new("static_hover_tips", "BANG_DREAM_LIB_SUBSIDE.description");

    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context)
    {
        yield return new CardDescriptionFragment(new LocString("cards", context.Card.Id.Entry + SubsidePostfix));
    }

    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        card.TryGetSecondaryCosts(out var set);
        if (set.ResourceIds.Contains(BangDreamConst.LingeredResource))
        {
            Description.Add("Amount", set.Get(BangDreamConst.LingeredResource).Amount);
            yield return new HoverTip(Title, Description);
        }
    }
}