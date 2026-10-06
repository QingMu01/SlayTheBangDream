using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;

namespace BangDreamLib.Scripts.Mechanics.Perform;

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


        var musicDescription = LocString.GetIfExists("cards",
            isInstant ? context.Card.Id.Entry + InstantPostfix : context.Card.Id.Entry + PerformPostfix);
        if (musicDescription != null)
        {
            context.Card.DynamicVars.AddTo(musicDescription);

            musicDescription.Add(new IfUpgradedVar(context.IsUpgradePreview ? UpgradeDisplay.UpgradePreview :
                context.Card.IsUpgraded ? UpgradeDisplay.Upgraded : UpgradeDisplay.Normal));

            var keywordText = isInstant ? Instant : Perform;
            keywordText.Add(new StringVar("Description", musicDescription.GetFormattedText()));

            yield return new CardDescriptionFragment(keywordText, CardDescriptionFragmentPlacement.BeforeBase);
        }
    }

    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        if (card is IPerformCard performCard)
        {
            yield return HoverTipFactory.FromKeyword(BangDreamConst.Music);
            yield return performCard.IsInstant
                ? HoverTipFactory.FromKeyword(BangDreamConst.Instant)
                : HoverTipFactory.FromKeyword(BangDreamConst.Perform);

            // 手动落位卡牌额外说明"出牌时要自己选落位"。
            switch (PerformTargetTypes.ResolveMode(card.TargetType))
            {
                case PerformSlotSelectionMode.Slot:
                    yield return HoverTipFactory.FromKeyword(BangDreamConst.AnySlot);
                    break;
                case PerformSlotSelectionMode.Group:
                    yield return HoverTipFactory.FromKeyword(BangDreamConst.RequestGroup);
                    break;
            }
        }
    }
}