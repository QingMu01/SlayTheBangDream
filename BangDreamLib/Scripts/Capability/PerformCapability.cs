using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Models.Capabilities;

namespace BangDreamLib.Scripts.Capability;

[RegisterModelCapability]
public class PerformCapability : CardCapability, ICardDescriptionContributor, ICardHoverTipContributor
{
    private const string InstantPostfix = ".instant";
    private const string PerformPostfix = ".perform";

    private static readonly LocString Perform = new("gameplay_ui", "BANG_DREAM_LIB_PERFORM_PLAY_TEXT");
    private static readonly LocString Instant = new("gameplay_ui", "BANG_DREAM_LIB_INSTANT_PLAY_TEXT");

    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context)
    {
        var isInstant = context.Card is IPerformCard { IsInstant: true };

        var musicDescription = isInstant
            ? new LocString("cards", context.Card.Id.Entry + InstantPostfix)
            : new LocString("cards", context.Card.Id.Entry + PerformPostfix);

        var finalDescription = isInstant ? Instant : Perform;

        context.Card.DynamicVars.AddTo(musicDescription);

        musicDescription.Add(new IfUpgradedVar(context.IsUpgradePreview ? UpgradeDisplay.UpgradePreview :
            context.Card.IsUpgraded ? UpgradeDisplay.Upgraded : UpgradeDisplay.Normal));

        finalDescription.Add(new StringVar("Description", musicDescription.GetFormattedText()));

        return [new CardDescriptionFragment(finalDescription)];
    }

    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        return card is IPerformCard { IsInstant: true }
            ? BangDreamConst.Instant.GetModKeywordHoverTips()
            : BangDreamConst.Perform.GetModKeywordHoverTips();
    }
}