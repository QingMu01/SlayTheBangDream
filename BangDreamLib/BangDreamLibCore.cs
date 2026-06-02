// See https://aka.ms/new-console-template for more information

using System.Reflection;
using BangDreamLib.Scripts.Character;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Features;
using BangDreamLib.Scripts.Features.Rules;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Patches;
using BangDreamLib.Scripts.Rewards;
using BangDreamLib.Scripts.Saved;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using Godot;
using ItsCrychic.Scripts.Patches;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.CardPiles;
using STS2RitsuLib.Combat.Rewards;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Utils.Persistence;
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

        var deckSupport = RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, "extra_card_and_deck_patches");
        deckSupport.RegisterPatches<SetupExtraDeckPatches>();
        deckSupport.RegisterPatches<MusicCardSupportPatches>();
        deckSupport.RegisterPatches<PopulateExtraDeckInCombatPatches>();
        if (!deckSupport.PatchAll())
        {
            throw new InvalidOperationException("music card patches failed.");
        }

        var preload = RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, "preload_assets");
        preload.RegisterPatches<PreloadPatches>();
        preload.PatchAll();

        var hiddenRelicSupport = RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, "hidden_relic_patches");
        hiddenRelicSupport.RegisterPatches<HiddenRelicPatchSet>();
        hiddenRelicSupport.PatchAll();

        var playerData = RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, "attache_player_data_patches");
        playerData.RegisterPatches<AttachePlayerExtraContentPatches>();
        playerData.PatchAll();

        var aggregationCharacter =
            RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, "aggregation_selector_patches");
        aggregationCharacter.RegisterPatches<AggregationSelectorPatches>();
        aggregationCharacter.PatchAll();

        var skinSupport = RitsuLibFramework.CreatePatcher(BangDreamConst.ModId, "skin_patches");
        skinSupport.RegisterPatches<SkinVisualSupportPatches>();
        skinSupport.RegisterPatches<SkinContentSupportPatches>();
        if (!skinSupport.PatchAll())
        {
            throw new InvalidOperationException("skins support patches failed.");
        }

        // 注册持久化数据
        using (RitsuLibFramework.BeginModDataRegistration(BangDreamConst.ModId))
        {
            var store = RitsuLibFramework.GetDataStore(BangDreamConst.ModId);

            store.Register(
                key: BangDreamConst.SaveKeySkin,
                fileName: "skin_config.json",
                scope: SaveScope.Profile,
                defaultFactory: () => new SavedSkin(),
                autoCreateIfMissing: true);
        }

        // 注册关键字
        var keywords = RitsuLibFramework.GetKeywordRegistry(BangDreamConst.ModId);
        keywords.RegisterCardKeywordOwnedByLocNamespace("Music");
        keywords.RegisterCardKeywordOwnedByLocNamespace("MusicNote");
        keywords.RegisterCardKeywordOwnedByLocNamespace("Performance");
        keywords.RegisterCardKeywordOwnedByLocNamespace("PerformanceArea");
        keywords.RegisterCardKeywordOwnedByLocNamespace("Linger");

        // 注册自定义奖励
        var customReward = ModRewardRegistry.For(BangDreamConst.ModId);
        customReward.RegisterOwned("RewardMusic",
            (save, player, _) =>
                new MusicCardReward(
                    new CardCreationOptions(save.CardPoolIds.Select(ModelDb.GetById<CardPoolModel>), save.Source,
                        save.RarityOdds), save.OptionCount, player));

        // 注册自定义牌堆
        var customPile = ModCardPileRegistry.For(BangDreamConst.ModId);
        customPile.RegisterOwned("ExtraDeck", new ModCardPileSpec
        {
            Scope = ModCardPileScope.RunPersistent,
            Style = ModCardPileUiStyle.TopBarDeck,
            IconPath = "res://BangDreamLib/images/sceneui/extra_deck.png",
            VisibleWhen = pileContext => pileContext.Player?.Character is IExtraDeckSupportCharacter
            {
                ShouldAlwaysShowExtraDeckAndPile: true
            }
        });
        customPile.RegisterOwned("ExtraDraw", new ModCardPileSpec
        {
            Scope = ModCardPileScope.CombatOnly,
            Style = ModCardPileUiStyle.BottomLeft,
            Anchor = ModCardPileAnchor.AtPosition(new Vector2(15f, 800f)),
            IconPath = "res://BangDreamLib/images/sceneui/music_draw.png",
            VisibleWhen = pileContext => pileContext.Player?.Character is IExtraDeckSupportCharacter
            {
                ShouldAlwaysShowExtraDeckAndPile: true
            }
        });
        customPile.RegisterOwned("Performance", new ModCardPileSpec
        {
            Scope = ModCardPileScope.CombatOnly,
            Style = ModCardPileUiStyle.Headless,
            FlightTargetPositionResolver = context =>
                context.CardModel?.Owner.AttachedData().PerformanceManager.PerformanceArea?.GetSlotPosWithAutoFlip() ??
                Vector2.Zero
        });

        // 注册公共内容
        var commonContent = RitsuLibFramework.GetContentRegistry(BangDreamConst.ModId);
        commonContent.RegisterSingleton<LingeredEnergyCounter>();
        commonContent.RegisterSingleton<CopySelfAndPlayCardRule>();

        commonContent.RegisterCharacter<PoppinParty>();
        commonContent.RegisterCharacter<Afterglow>();
        commonContent.RegisterCharacter<PastelPalettes>();
        commonContent.RegisterCharacter<Roselia>();
        commonContent.RegisterCharacter<HelloHappyWorld>();
        commonContent.RegisterCharacter<Morfonica>();
        commonContent.RegisterCharacter<RaiseASuilen>();
        commonContent.RegisterCharacter<MyGo>();
        commonContent.RegisterCharacter<AveMujica>();
        commonContent.RegisterCharacter<YumemitaMewType>();
        commonContent.RegisterCharacter<Millsage>();
        commonContent.RegisterCharacter<IkaDumbRock>();
        commonContent.RegisterCharacter<Crychic>();

        // 预加载皮肤资源
        RitsuLibFramework.SubscribeLifecycle<ModelPreloadingCompletedEvent>(_ =>
        {
            foreach (var character in ModelDb.AllCharacters.OfType<ISkinSupportCharacter>().ToList())
            {
                foreach (var skinPath in character.CharacterSkinList)
                {
                    var skinTemplate = BangDreamTools.LoadFromJson<SkinTemplate>(skinPath);
                    if (skinTemplate != null)
                    {
                        SkinManager.RegisterCharacterSkin(character.GetType(), skinTemplate);
                    }
                    else
                    {
                        Logger.Error($"Failed to load skin template from {skinPath}");
                    }
                }
            }
        });
        //设置乐队角色选择按钮可视化
        RitsuLibFramework.SubscribeLifecycle<ModelRegistryInitializedEvent>(_ =>
        {
            InitBandSelectIcons();
            var characterButtons = ModelDb.AllCharacters.OfType<IAggregationGroup>().ToList();
            var groupCharacters = ModelDb.AllCharacters.OfType<IAggregationCharacter>().ToList();
            foreach (var button in characterButtons)
            {
                if (button is BangDreamGroup bandGroup)
                {
                    var members = groupCharacters.Where(item => item.Band == bandGroup.Band).ToList();
                    var hasAnySelectableMember = members.Any(item => item.AllowSelect);
                    var hasVisibleMember = members.Any(item => !item.IsHidden);
                    bandGroup.HasEffectiveMember = hasVisibleMember && hasAnySelectableMember;
                }
            }
        });

        ModHelper.SubscribeForCombatStateHooks("ExtraSubscribe",
            state =>
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

    private static void InitBandSelectIcons()
    {
    }
}