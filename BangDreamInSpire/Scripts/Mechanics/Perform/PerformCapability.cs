using BangDreamInSpire.Scripts.Cards;
using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;

namespace BangDreamInSpire.Scripts.Mechanics.Perform;

[RegisterModelCapability]
public class PerformCapability : CardCapability, ICardDescriptionContributor, ICardHoverTipContributor
{
    private const string InstantPostfix = ".instant";
    private const string PerformPostfix = ".perform";

    private static readonly LocString Perform = new("gameplay_ui", "BANG_DREAM_IN_SPIRE_PERFORM_PLAY_TEXT");
    private static readonly LocString Instant = new("gameplay_ui", "BANG_DREAM_IN_SPIRE_INSTANT_PLAY_TEXT");

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

            switch (PerformTargetTypes.ResolveMode(card.TargetType))
            {
                case PerformSlotSelectionMode.Slot:
                    yield return HoverTipFactory.FromKeyword(BangDreamConst.AnySlot);
                    break;
                case PerformSlotSelectionMode.Group when HasMultipleGroups(card):
                    yield return HoverTipFactory.FromKeyword(BangDreamConst.RequestGroup);
                    break;
            }
        }
    }

    /// <summary>该卡是否会出现分组选择，即是否存在多个可达分组。</summary>
    private static bool HasMultipleGroups(CardModel card)
    {
        if (card is { IsMutable: true, Owner.Character: IPerformableCharacter ownerPerformable })
        {
            return ownerPerformable.CreatePerformScheme().Groups.Count > 1;
        }

        return MusicCardModel.ResolveOriginCharacter(card) is IPerformableCharacter performable &&
               performable.CreatePerformScheme().Groups.Count > 1;
    }
}