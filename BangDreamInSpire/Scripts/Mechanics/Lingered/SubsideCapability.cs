using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;

namespace BangDreamInSpire.Scripts.Mechanics.Lingered;

[RegisterModelCapability]
public class SubsideCapability : CardCapability, ICardDescriptionContributor, ICardHoverTipContributor,
    ICardGlowContributor, ICardOverlayContributor
{
    private const string SubsidePostfix = ".subside";

    // LocString 的 Add 是写内部字典：不能把可变实例做成 static，否则同一帧渲染多张余音卡时变量互相覆盖。
    private static readonly LocString Title = new("static_hover_tips", "BANG_DREAM_IN_SPIRE_SUBSIDE.title");

    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context)
    {
        var subsideDescription = new LocString("cards", context.Card.Id.Entry + SubsidePostfix);

        context.Card.DynamicVars.AddTo(subsideDescription);

        subsideDescription.Add(new IfUpgradedVar(context.IsUpgradePreview ? UpgradeDisplay.UpgradePreview :
            context.Card.IsUpgraded ? UpgradeDisplay.Upgraded : UpgradeDisplay.Normal));

        var pileType = context.Card.Pile?.Type;
        if (pileType != null)
        {
            subsideDescription.Add("OnTable", pileType is PileType.Hand or PileType.Play);
        }
        else
        {
            subsideDescription.Add("OnTable", false);
        }


        var text = new LocString("gameplay_ui", "BANG_DREAM_IN_SPIRE_SUBSIDE_PLAY_TEXT");
        text.Add(new StringVar("Description", subsideDescription.GetFormattedText()));
        yield return new CardDescriptionFragment(text);
    }

    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        card.TryGetSecondaryCosts(out var set);
        if (set.ResourceIds.Contains(BangDreamConst.LingeredResource))
        {
            var description = new LocString("static_hover_tips", "BANG_DREAM_IN_SPIRE_SUBSIDE.description");
            description.Add("Amount", SecondaryResourcePaymentResolver.Plan(card).Lines
                .Where(line => line.ResourceId.Equals(BangDreamConst.LingeredResource))
                .Sum(line => line.Value));
            yield return new HoverTip(Title, description);
        }
    }

    public bool ShouldGlowGold(CardModel card)
    {
        return LingeredResourcesRule.IsSufficient(card);
    }

    public IEnumerable<CardOverlayContribution> GetCardOverlays(CardOverlayContext context)
    {
        if (context.Card is ISubsideCard)
        {
            var factory = CardOverlayRegistry.Resolve("head_tip");
            if (factory != null)
            {
                return [CardOverlayContribution.FromFactory("head_tip", factory)];
            }
        }

        return [];
    }
}