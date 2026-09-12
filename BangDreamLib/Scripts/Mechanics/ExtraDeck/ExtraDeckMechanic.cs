using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.CardPiles;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.RunData;

namespace BangDreamLib.Scripts.Mechanics.ExtraDeck;

/// <summary>
/// 额外卡组机制：额外卡池/额外卡组/额外抽牌堆、小祥商人、额外卡组删牌、音乐牌奖励。
/// 由实现 <see cref="IExtraDeckSupportCharacter" /> 的角色启用。
/// </summary>
public sealed class ExtraDeckMechanic : IBangDreamMechanic
{
    public string Id => "extra_deck";

    public int Order => 100;

    public IReadOnlyList<Type> RequiredCharacterCapabilities => [typeof(IExtraDeckSupportCharacter)];

    public void RegisterContent(BangDreamMechanicContext context)
    {
        BangDreamConst.ExtraCardMerchant = context.RunData.RegisterPerPlayer(
            key: BangDreamConst.RunDataKeyExtraCardMerchant,
            defaultFactory: () => new ExtraCardMerchantData(),
            options: new RunSavedDataOptions
            {
                WritePolicy = RunSavedDataWritePolicy.AlwaysWhenRegistered
            });

        BangDreamConst.ExtraDeck = context.CardPiles.RegisterOwned("ExtraDeck", new ModCardPileSpec
        {
            Scope = ModCardPileScope.RunPersistent,
            Style = ModCardPileUiStyle.TopBarDeck,
            IconPath = "res://BangDreamLib/images/sceneui/extra_deck.png",
            VisibleWhen = pileContext =>
            {
                if (pileContext.Player?.Character is IExtraDeckSupportCharacter { ShouldAlwaysShowExtraDeck: true })
                {
                    return true;
                }

                return pileContext.Pile?.Cards.Any() ?? false;
            }
        }).PileType;

        BangDreamConst.ExtraDraw = context.CardPiles.RegisterOwned("ExtraDraw", new ModCardPileSpec
        {
            Scope = ModCardPileScope.CombatOnly,
            Style = ModCardPileUiStyle.BottomLeft,
            Anchor = ModCardPileAnchor.AtPosition(new Vector2(15f, 800f)),
            IconPath = "res://BangDreamLib/images/sceneui/music_draw.png",
            VisibleWhen = pileContext =>
            {
                if (pileContext.Player?.Character is IExtraDeckSupportCharacter { ShouldAlwaysShowExtraPile: true })
                {
                    return true;
                }

                return pileContext.Player?.PlayerCombatState?.AllCards.Any(card => card is IPerformCard) ?? false;
            }
        }).PileType;

        BangDreamConst.RewardMusic = context.Rewards.RegisterOwned("MusicCardReward",
            (save, player, _) => new MusicCardReward(
                new CardCreationOptions(save.CardPoolIds.Select(ModelDb.GetById<CardPoolModel>),
                    save.Source, save.RarityOdds),
                save.OptionCount, player)).RewardType;
    }

    public void RegisterPatches(ModPatcher patcher)
    {
        patcher.RegisterPatches<ExtraCardMerchantPatches>();
        patcher.RegisterPatches<ExtraDeckPoolPatches>();
        patcher.RegisterPatch<PopulateExtraDeckInCombatPatch>();
    }
}
