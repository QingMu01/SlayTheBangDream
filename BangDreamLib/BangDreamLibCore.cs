using System.Reflection;
using BangDreamLib.Scripts.Character;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Features;
using BangDreamLib.Scripts.Features.Rules;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Multiplayer.RunData;
using BangDreamLib.Scripts.Patches;
using BangDreamLib.Scripts.Rewards;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.CardPiles;
using STS2RitsuLib.CardTags;
using STS2RitsuLib.Combat.Rewards;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.RunData;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace BangDreamLib;

[ModInitializer(nameof(Initialize))]
public class BangDreamLibCore
{
    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(BangDreamConst.ModId);

    public static void Initialize()
    {
        var executingAssembly = Assembly.GetExecutingAssembly();
        RitsuLibFramework.EnsureGodotScriptsRegistered(executingAssembly, Logger);

        var preload = RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, "preload_assets");
        preload.RegisterPatches<PreloadPatches>();
        preload.PatchAll();

        var deckSupport = RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, "extra_deck_support");
        deckSupport.RegisterPatches<MusicCardSupportPatches>();
        if (!deckSupport.PatchAll())
        {
            throw new InvalidOperationException("music card patches failed.");
        }

        var vfxManager = RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, "async_attack_vfx_manager");
        vfxManager.RegisterPatches<VfxManagerPatches>();
        if (!vfxManager.PatchAll())
        {
            throw new InvalidOperationException("bang dream combat vfx container patches failed.");
        }

        var submenuSupport = RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, "character_selector_submenu");
        submenuSupport.RegisterPatches<GroupableCharacterSelectorPatches>();
        if (!submenuSupport.PatchAll())
        {
            throw new InvalidOperationException("character selector submenu patches failed.");
        }

        var vanillaKeyword = RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, "keyword_vanilla_style");
        vanillaKeyword.RegisterPatches<CardKeywordPatches>();
        if (!vanillaKeyword.PatchAll())
        {
            throw new InvalidOperationException("mod keyword vanilla style patches failed.");
        }

        var skinSupport = RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, "skin_support");
        skinSupport.RegisterPatches<SkinVisualSupportPatches>();
        skinSupport.RegisterPatches<SkinStartingContentPopulatePatches>();
        if (!skinSupport.PatchAll())
        {
            throw new InvalidOperationException("skins support patches failed.");
        }

        // 注册持久化数据
        using (RitsuLibFramework.BeginModDataRegistration(BangDreamConst.ModId))
        {
            var store = RitsuLibFramework.GetRunSavedDataStore(BangDreamConst.ModId);

            BangDreamConst.PlayerSkin = store.RegisterPerPlayer(
                key: BangDreamConst.RunDataKeySkin,
                defaultFactory: () => new PlayerSkinData(),
                options: new RunSavedDataOptions
                {
                    WritePolicy = RunSavedDataWritePolicy.AlwaysWhenRegistered,
                    SyncLobbyOnChange = true
                });
        }

        // 注册关键字
        var keywords = RitsuLibFramework.GetKeywordRegistry(BangDreamConst.ModId);
        BangDreamConst.Music = RegisterKeyword(keywords, "Music");
        BangDreamConst.Lingered = RegisterKeyword(keywords, "Lingered");
        BangDreamConst.Instant = RegisterKeyword(keywords, "Instant");
        BangDreamConst.MusicNote = RegisterKeyword(keywords, "MusicNote");
        BangDreamConst.Perform = RegisterKeyword(keywords, "Perform");
        BangDreamConst.PerformArea = RegisterKeyword(keywords, "PerformArea");

        // 注册自定义标签
        var cardTagRegistry = ModCardTagRegistry.For(BangDreamConst.ModId);
        BangDreamConst.SymbolCard = RegisterCardTag(cardTagRegistry, "Symbol");

        // 注册自定义奖励
        var customReward = ModRewardRegistry.For(BangDreamConst.ModId);
        BangDreamConst.RewardMusic = customReward.RegisterOwned("RewardMusic",
            (save, player, _) => new MusicCardReward(
                new CardCreationOptions(save.CardPoolIds.Select(ModelDb.GetById<CardPoolModel>),
                    save.Source, save.RarityOdds),
                save.OptionCount, player)).RewardType;

        // 注册自定义牌堆
        var customPile = ModCardPileRegistry.For(BangDreamConst.ModId);
        BangDreamConst.ExtraDeck = customPile.RegisterOwned("ExtraDeck", new ModCardPileSpec
        {
            Scope = ModCardPileScope.RunPersistent,
            Style = ModCardPileUiStyle.TopBarDeck,
            IconPath = "res://BangDreamLib/images/sceneui/extra_deck.png",
            VisibleWhen = context =>
            {
                if (context.Player?.Character is IExtraDeckSupportCharacter { ShouldAlwaysShowExtraDeck: true })
                {
                    return true;
                }

                return context.Pile?.Cards.Any() ?? false;
            }
        }).PileType;

        BangDreamConst.ExtraDraw = customPile.RegisterOwned("ExtraDraw", new ModCardPileSpec
        {
            Scope = ModCardPileScope.CombatOnly,
            Style = ModCardPileUiStyle.BottomLeft,
            Anchor = ModCardPileAnchor.AtPosition(new Vector2(15f, 800f)),
            IconPath = "res://BangDreamLib/images/sceneui/music_draw.png",
            VisibleWhen = context =>
            {
                if (context.Player?.Character is IExtraDeckSupportCharacter { ShouldAlwaysShowExtraPile: true })
                {
                    return true;
                }

                return context.Player?.PlayerCombatState?.AllCards.Any(card => card is IPerformanceCard) ?? false;
            }
        }).PileType;

        BangDreamConst.PerformPile = customPile.RegisterOwned("Performance", new ModCardPileSpec
        {
            Scope = ModCardPileScope.CombatOnly,
            Style = ModCardPileUiStyle.Headless,
            FlightStartPositionResolver = context =>
                context.CardModel?.Owner.AttachedData().PerformanceManager.PerformanceArea
                    ?.GetPilePost(context.CardModel),
            FlightTargetPositionResolver = context =>
                context.CardModel?.Owner.AttachedData().PerformanceManager.PerformanceArea
                    ?.GetPilePost(context.CardModel)
        }).PileType;

        // 注册余音资源
        var registry = RitsuLibFramework.GetSecondaryResourceRegistry(BangDreamConst.ModId);

        BangDreamConst.LingeredResource = registry.Register("Lingered", new SecondaryResourceDefinition(
            defaultAmount: 0,
            baseMaxAmount: 7,
            turnStartPolicy: SecondaryResourceTurnStartPolicy.None,
            persistencePolicy: SecondaryResourcePersistencePolicy.None,
            smallIconPath: "res://Test/images/resources/mana_small.png",
            largeIconPath: "res://Test/images/resources/mana_large.png"
        )).Id;

        // 注册公共内容
        var commonContent = RitsuLibFramework.GetContentRegistry(BangDreamConst.ModId);
        commonContent.RegisterCharacter<GroupCharacterPlaceholder>();
        commonContent.RegisterCharacterStarterRelic<GroupCharacterPlaceholder, Circlet>();

        commonContent.RegisterSingleton<LingeredEnergyCounter>();
        commonContent.RegisterSingleton<CopySelfAndPlayCardRule>();

        // 预加载皮肤资源
        RitsuLibFramework.SubscribeLifecycle<ModelPreloadingCompletedEvent>(_ =>
        {
            foreach (var character in ModelDb.AllCharacters.OfType<ISkinSupportCharacter>())
            {
                foreach (var skinPath in character.CharacterSkinList)
                {
                    var skinTemplate = BangDreamTools.LoadFromJson<SkinTemplate>(skinPath);
                    if (skinTemplate != null)
                    {
                        SkinManager.RegisterCharacterSkin(skinPath, skinTemplate);
                    }
                    else
                    {
                        Logger.Error($"Failed to load skin template from {skinPath}");
                    }
                }
            }
        });

        ModHelper.SubscribeForCombatStateHooks("ExtraSubscribe", state =>
        {
            var subscribeModels = new List<AbstractModel>
            {
                ModelDb.Singleton<LingeredEnergyRule>(),
                ModelDb.Singleton<CopySelfAndPlayCardRule>()
            };
            subscribeModels.AddRange(state.Players.Select(player => player.AttachedData().LingeredEnergy));
            subscribeModels.AddRange(state.Players.Select(player => player.AttachedData().PerformanceManager));
            subscribeModels.AddRange(state.Players.Select(player => player.AttachedData().MusicNoteDamageTracker));
            return subscribeModels;
        });
    }

    private static CardKeyword RegisterKeyword(ModKeywordRegistry registry, string keyword)
    {
        return registry.RegisterCardKeywordOwnedByLocNamespace(keyword).CardKeywordValue;
    }

    private static CardTag RegisterCardTag(ModCardTagRegistry registry, string tag)
    {
        return registry.RegisterOwned(tag).CardTagValue;
    }
}