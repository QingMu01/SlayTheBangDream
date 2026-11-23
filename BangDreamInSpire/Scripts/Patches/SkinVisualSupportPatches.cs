using System.Reflection;
using BangDreamInSpire.Scripts.Cards;
using BangDreamInSpire.Scripts.Diagnostics;
using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Nodes.MegeScript;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.TreasureRelicPicking;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Events.Custom;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.CardPiles;
using STS2RitsuLib.CardPiles.Nodes;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Utils.HarmonyIl;

namespace BangDreamInSpire.Scripts.Patches;

public class SkinVisualSupportPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<CreatureVisualsPatch>();
        patcher.RegisterPatch<EnergyCounterPatch>();
        patcher.RegisterPatch<RestSiteScenePatch>();
        patcher.RegisterPatch<RestSiteAnimPatch>();
        patcher.RegisterPatch<MerchantScenePatch>();
        patcher.RegisterPatch<FakeMerchantScenePatch>();
        patcher.RegisterPatch<ArmPointingTexturePatch>();
        patcher.RegisterPatch<ArmFightTexturePatch>();
        patcher.RegisterPatch<MusicCardFramePatch>();
        patcher.RegisterPatch<MusicCardFrameMaterialPatch>();
        patcher.RegisterPatch<MapMarkerPatch>();
        patcher.RegisterPatch<TopBarPatch>();
        patcher.RegisterPatch<TopBarExtraDeckPatch>();
        patcher.RegisterPatch<CombatCardPilePatch>();
        patcher.RegisterPatch<CardRewardPatch>();
    }
}

internal class CreatureVisualsPatch : IPatchMethod
{
    public static string PatchId => "create_creature_visuals_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(Creature), nameof(Creature.CreateVisuals))];
    }

    /// <summary>统一在 Creature.CreateVisuals 应用玩家皮肤，覆盖战斗外死亡等直接调用；
    /// 不要再补 NCreature.Create，避免重复实例。</summary>
    public static void Postfix(Creature __instance, ref NCreatureVisuals? __result)
    {
        // TestMode 或无 Player 时保留本体结果。
        if (__result == null || __instance.Player is not { } player)
        {
            return;
        }

        var path = SkinManager.ResolvePlayerVisualScene(player);
        if (path == null)
        {
            return;
        }

        var placeholder = __result;
        __result = RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(path);

        // 未入树的占位视觉直接回收。
        if (GodotObject.IsInstanceValid(placeholder))
        {
            // 未入树节点直接 Free，不依赖 QueueFree 时机。
            placeholder.Free();
        }
    }
}

internal class EnergyCounterPatch : IPatchMethod
{
    public static string PatchId => "create_energy_counter_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NEnergyCounter), nameof(NEnergyCounter.Create))];
    }

    public static void Postfix(Player player, ref NEnergyCounter? __result)
    {
        if (player.Character is ISkinSupportCharacter)
        {
            var path = SkinManager.ResolvePlayerSkin(player)?.SkinTemplate.Ui
                .EnergyCounterScene;
            if (path != null)
            {
                var energyCounter = RitsuGodotNodeFactories.CreateFromScenePath<NEnergyCounter>(path);
                Traverse.Create(energyCounter).Field("_player").SetValue(player);
                if (__result != null && GodotObject.IsInstanceValid(__result))
                {
                    // 替换后立即回收占位节点。
                    __result.Free();
                }

                __result = energyCounter;
            }
        }
    }
}

internal class RestSiteScenePatch : IPatchMethod
{
    public static string PatchId => "create_rest_site_by_skin_info";

    private static readonly PropertyInfo CachePlayer = AccessTools.Property(typeof(NRestSiteCharacter), "Player");
    private static readonly FieldInfo CacheIndex = AccessTools.Field(typeof(NRestSiteCharacter), "_characterIndex");

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter.Create))];
    }

    public static void Postfix(Player player, int characterIndex, ref NRestSiteCharacter? __result)
    {
        if (player.Character is ISkinSupportCharacter)
        {
            var path = SkinManager.ResolvePlayerSkin(player)?.SkinTemplate.MultiplayerVisual.RestSiteScene;
            if (path != null)
            {
                var restSiteCharacter = RitsuGodotNodeFactories.CreateFromScenePath<NRestSiteCharacter>(path);

                CachePlayer.SetValue(restSiteCharacter, player);
                CacheIndex.SetValue(restSiteCharacter, characterIndex);
                if (__result != null && GodotObject.IsInstanceValid(__result))
                {
                    __result.Free();
                }

                __result = restSiteCharacter;
            }
        }
    }
}

internal class RestSiteAnimPatch : IPatchMethod
{
    public static string PatchId => "create_rest_site_anim_by_skin_info";

    private const string AnimNameFieldName = "animName";

    private static readonly Func<CodeInstruction, bool> IsAnyLdloc = HarmonyIl.IsLdloc();
    private static readonly Func<CodeInstruction, bool> IsAnyStfld = HarmonyIl.IsStfld();

    private static readonly MethodInfo GetRestSiteAnimNameMethod =
        AccessTools.Method(typeof(RestSiteAnimPatch), nameof(GetRestSiteAnimName));

    private static readonly MethodInfo GetPlayerMethod =
        AccessTools.PropertyGetter(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter.Player));

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter._Ready))];
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var rewriter = HarmonyIlRewriter.From(instructions);

        var matches = FindAnimNameAssignments(rewriter.Code);

        if (matches.Count == 1)
        {
            var match = matches[0];
            rewriter.Replace(match, [
                rewriter.Code[match.Index].Clone(),
                rewriter.Code[match.Index + 1].Clone(),
                HarmonyIl.Ldarg(0),
                HarmonyIl.Call(GetPlayerMethod),
                HarmonyIl.Call(GetRestSiteAnimNameMethod),
                rewriter.Code[match.Index + 2].Clone()
            ]);
            return rewriter.InstructionsChecked("Replace rest site animName assignment");
        }

        BangDreamInSpireCore.Logger.Error(
            $"Replace rest site animName transpiler failed to match. found {matches.Count} candidates.");
        // 失配写入诊断时间线。
        PerfTimeline.Mark("patch:rest-site-anim:unmatched", $"candidates={matches.Count}");
        return rewriter.Code;
    }

    /// <summary>按 animName 字段与 ldloc 指令定位赋值，不依赖局部变量槽位。</summary>
    private static List<HarmonyIlMatch> FindAnimNameAssignments(IReadOnlyList<CodeInstruction> code)
    {
        var matches = new List<HarmonyIlMatch>();
        for (var i = 2; i < code.Count; i++)
        {
            if (!IsAnyStfld(code[i]) || code[i].operand is not FieldInfo field ||
                field.Name != AnimNameFieldName || field.FieldType != typeof(string) ||
                !IsAnyLdloc(code[i - 1]) || !IsAnyLdloc(code[i - 2]))
            {
                continue;
            }

            matches.Add(new HarmonyIlMatch(i - 2, 3));
        }

        return matches;
    }

    private static string GetRestSiteAnimName(string originalName, Player player)
    {
        if (player.Character is ISkinSupportCharacter)
        {
            return SkinManager.ResolvePlayerSkin(player)?.SkinTemplate.MultiplayerVisual.RestSiteAnimName ??
                   originalName;
        }

        return originalName;
    }
}

internal class MerchantScenePatch : IPatchMethod
{
    public static string PatchId => "create_merchant_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NMerchantRoom), "AfterRoomIsLoaded")];
    }

    public static void Postfix(List<Player> ____players, List<NMerchantCharacter> ____playerVisuals,
        Control ____characterContainer)
    {
        var players = ____players.ToList();
        players.Reverse();
        var nodeContainer = ____characterContainer.GetChildren().OfType<NMerchantCharacter>().ToList();
        // 容器与玩家数不一致时夹取。
        var count = Math.Min(players.Count, nodeContainer.Count);
        for (var i = 0; i < count; i++)
        {
            var currentPlayer = players[i];
            var merchantCharacter = BangDreamMerchant.Create(currentPlayer);
            if (merchantCharacter != null)
            {
                var currentNode = nodeContainer[i];
                var index = currentNode.GetIndex();
                ____characterContainer.AddChild(merchantCharacter);
                ____characterContainer.MoveChild(merchantCharacter, index);
                var indexOf = ____playerVisuals.IndexOf(currentNode);
                if (indexOf != -1)
                {
                    ____playerVisuals.RemoveAt(indexOf);
                    ____playerVisuals.Insert(indexOf, merchantCharacter);
                }

                currentNode.QueueFree();
            }
        }
    }
}

internal class FakeMerchantScenePatch : IPatchMethod
{
    public static string PatchId => "create_fake_merchant_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NFakeMerchant), "AfterRoomIsLoaded")];
    }

    public static void Postfix(List<Player> ____players, Control ____characterContainer)
    {
        var players = ____players.ToList();
        players.Reverse();

        // NFakeMerchant 使用 NCreatureVisuals，容器顺序与玩家相反。
        var nodeContainer = ____characterContainer.GetChildren().OfType<NCreatureVisuals>().ToList();
        var count = Math.Min(players.Count, nodeContainer.Count);
        for (var i = 0; i < count; i++)
        {
            var currentPlayer = players[i];

            // 优先使用商人皮肤，缺省回退角色视觉。
            Node? replacement = BangDreamMerchant.Create(currentPlayer);
            if (replacement == null)
            {
                var visualScene = SkinManager.ResolvePlayerVisualScene(currentPlayer);
                if (visualScene == null)
                {
                    continue;
                }

                replacement = RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(visualScene);
            }

            var currentNode = nodeContainer[i];
            var index = currentNode.GetIndex();
            ____characterContainer.AddChild(replacement);
            ____characterContainer.MoveChild(replacement, index);
            currentNode.QueueFree();
        }
    }
}

internal class ArmPointingTexturePatch : IPatchMethod
{
    public static string PatchId => "create_arm_pointing_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NHandImage), nameof(NHandImage._Ready))];
    }

    public static void Postfix(NHandImage __instance, TextureRect ____textureRect)
    {
        if (__instance.Player.Character is ISkinSupportCharacter)
        {
            var path = SkinManager.ResolvePlayerSkin(__instance.Player)?.SkinTemplate.MultiplayerVisual
                .ArmPointingTexture;
            if (path != null)
            {
                ____textureRect.Texture = BangDreamPreloadManager.GetTexture2D(path);
            }
        }
    }
}

internal class ArmFightTexturePatch : IPatchMethod
{
    public static string PatchId => "create_fight_arm_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NHandImage), "SetTextureToFightMove")];
    }

    public static void Postfix(NHandImage __instance, RelicPickingFightMove move, TextureRect ____textureRect)
    {
        if (__instance.Player.Character is ISkinSupportCharacter)
        {
            var armTextureSet = SkinManager.ResolvePlayerSkin(__instance.Player)?.SkinTemplate
                .MultiplayerVisual;
            var path = move switch
            {
                RelicPickingFightMove.Paper => armTextureSet?.ArmPaperTexture,
                RelicPickingFightMove.Rock => armTextureSet?.ArmRockTexture,
                RelicPickingFightMove.Scissors => armTextureSet?.ArmScissorsTexture,
                _ => null
            };
            if (path != null)
            {
                ____textureRect.Texture = BangDreamPreloadManager.GetTexture2D(path);
            }
        }
    }
}
internal static class MusicCardSkinFrames
{
    public static bool TryResolveFramePath(CardModel card, out string path)
    {
        path = string.Empty;
        if (card is not MusicCardModel { IsMutable: true })
        {
            return false;
        }

        var origin = MusicCardModel.ResolveOriginCharacter(card);
        if (origin == null)
        {
            return false;
        }

        var framePath = ResolveSkin(card, origin)?.SkinTemplate.Ui.MusicCardFrame;
        if (string.IsNullOrEmpty(framePath) || !ResourceLoader.Exists(framePath))
        {
            return false;
        }

        path = framePath;
        return true;
    }

    private static SkinInfo? ResolveSkin(CardModel card, ISkinSupportCharacter origin)
    {
        // 使用来源角色玩家的皮肤。
        if (card.Owner is { RunState: RunState runState })
        {
            foreach (var player in runState.Players)
            {
                if (ReferenceEquals(player.Character, origin))
                {
                    return SkinManager.ResolvePlayerSkin(player);
                }
            }
        }

        // 来源角色不在本局时使用首个注册皮肤。
        foreach (var skinPath in origin.CharacterSkinList)
        {
            if (SkinManager.GetSkinInfo(skinPath) is { } defaultSkin)
            {
                return defaultSkin;
            }
        }

        return null;
    }
}

internal class MusicCardFramePatch : IPatchMethod
{
    public static string PatchId => "create_music_card_frame_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCard), "UpdatePortrait")];
    }

    // 必须在 RitsuLib 默认卡框刷新之后执行。
    [HarmonyPriority(Priority.Last)]
    public static void Postfix(NCard __instance, ref TextureRect? ____frame)
    {
        if (__instance.Model is not { } model || !MusicCardSkinFrames.TryResolveFramePath(model, out var path))
        {
            return;
        }

        ____frame ??= new TextureRect();
        ____frame.Texture = BangDreamPreloadManager.GetTexture2D(path);
    }
}

internal class MusicCardFrameMaterialPatch : IPatchMethod
{
    public static string PatchId => "clear_music_card_frame_material_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(CardModel), "FrameMaterial", MethodType.Getter)];
    }

    public static void Postfix(CardModel __instance, ref Material __result)
    {
        // 替换卡框时同步清理池染色材质。
        if (MusicCardSkinFrames.TryResolveFramePath(__instance, out _))
        {
            __result = null!;
        }
    }
}

internal class MapMarkerPatch : IPatchMethod
{
    public static string PatchId => "create_map_marker_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NMapMarker), nameof(NMapMarker.Initialize))];
    }

    public static void Postfix(NMapMarker __instance, Player player)
    {
        if (player.Character is ISkinSupportCharacter)
        {
            var path = SkinManager.ResolvePlayerSkin(player)?.SkinTemplate.Ui.MapMarker;
            if (path != null)
            {
                __instance.Texture = BangDreamPreloadManager.GetTexture2D(path);
            }
        }
    }
}

internal class TopBarPatch : IPatchMethod
{
    public static string PatchId => "create_top_bar_ui_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NTopBar), nameof(NTopBar.Initialize))];
    }

    public static void Postfix(NTopBar __instance, IRunState runState)
    {
        var player = LocalContext.GetMe(runState);
        if (player?.Character is not ISkinSupportCharacter)
        {
            return;
        }

        var ui = SkinManager.ResolvePlayerSkin(player)?.SkinTemplate.Ui;
        if (ui == null)
        {
            return;
        }

        var attempted = 0;
        var applied = 0;

        // 场景路径缺失时仅告警一次。
        ApplyIcon(__instance.Hp, "HpIcon", ui.TopBarHpIcon, ref attempted, ref applied);
        ApplyIcon(__instance.Gold, "GoldIcon", ui.TopBarGoldIcon, ref attempted, ref applied);
        ApplyIcon(__instance.FloorIcon, "FloorIconPositioner/FloorInfoIcon", ui.TopBarFloorIcon, ref attempted,
            ref applied);
        ApplyIcon(__instance.Map, "Control/Icon", ui.TopBarMapIcon, ref attempted, ref applied);
        ApplyIcon(__instance.Deck, "Control/Icon", ui.TopBarDeckIcon, ref attempted, ref applied);
        ApplyIcon(__instance.Pause, "Control/Icon", ui.TopBarSettingIcon, ref attempted, ref applied);

        if (attempted > 0 && applied == 0)
        {
            WarnMissingNodesOnce();
        }
    }

    private static int _missingNodesWarned;

    private static void ApplyIcon(Node root, string nodePath, string? iconPath, ref int attempted, ref int applied)
    {
        if (iconPath == null)
        {
            return;
        }

        attempted++;
        var textureRect = root.GetNodeOrNull<TextureRect>(nodePath);
        if (textureRect == null)
        {
            return;
        }

        textureRect.Texture = BangDreamPreloadManager.GetTexture2D(iconPath);
        applied++;
    }

    private static void WarnMissingNodesOnce()
    {
        if (Interlocked.Exchange(ref _missingNodesWarned, 1) != 0)
        {
            return;
        }

        BangDreamInSpireCore.Logger.Warn(
            $"[{PatchId}] 顶栏皮肤图标没有命中任何场景节点：" +
            "6 条硬编码子节点路径可能已随本体场景改名（本次运行只提示一次）。");
    }
}

internal class TopBarExtraDeckPatch : IPatchMethod
{
    public static string PatchId => "create_top_bar_extra_deck_ui_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NModCardPileButton), nameof(NModCardPileButton.Initialize))];
    }

    public static void Postfix(NModCardPileButton __instance, Player player, Control ____icon)
    {
        if (__instance.Definition is { Id: "BANG_DREAM_IN_SPIRE_CARDPILE_EXTRA_DECK", Style: ModCardPileUiStyle.TopBarDeck }
            && LocalContext.IsMe(player) && player.Character is ISkinSupportCharacter)
        {
            var path = SkinManager.ResolvePlayerSkin(player)?.SkinTemplate.Ui.TopBarExtraDeckIcon;
            if (path != null && ____icon is TextureRect textureRect)
            {
                textureRect.Texture = BangDreamPreloadManager.GetTexture2D(path);
            }
        }
    }
}

internal class CombatCardPilePatch : IPatchMethod
{
    public static string PatchId => "create_pile_button_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCombatCardPile), nameof(NCombatCardPile.Initialize))];
    }

    public static void Postfix(NCombatCardPile __instance, Player player, Control ____icon)
    {
        if (player.Character is ISkinSupportCharacter)
        {
            if (__instance is NDrawPileButton)
            {
                var path = SkinManager.ResolvePlayerSkin(player)?.SkinTemplate.Ui.CombatDrawIcon;
                if (path != null && ____icon is TextureRect textureRect)
                {
                    textureRect.Texture = BangDreamPreloadManager.GetTexture2D(path);
                }
            }
            else if (__instance is NDiscardPileButton)
            {
                var path = SkinManager.ResolvePlayerSkin(player)?.SkinTemplate.Ui.CombatDiscardIcon;
                if (path != null && ____icon is TextureRect textureRect)
                {
                    textureRect.Texture = BangDreamPreloadManager.GetTexture2D(path);
                }
            }
        }
    }
}

internal class CardRewardPatch : IPatchMethod
{
    public static string PatchId => "create_card_reward_icon_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(CardReward), "IconPath", MethodType.Getter)];
    }

    public static void Postfix(CardReward __instance, List<CardCreationResult> ____cards, ref string __result)
    {
        if (LocalContext.IsMe(__instance.Player) && __instance.Player.Character is ISkinSupportCharacter)
        {
            var ui = SkinManager.ResolvePlayerSkin(__instance.Player)?.SkinTemplate.Ui;
            if (ui != null)
            {
                // 无候选时保留本体图标，避免越界。
                if (____cards.Count == 0)
                {
                    return;
                }

                var newIconPath = ____cards[0].Card.Rarity switch
                {
                    CardRarity.Uncommon => ui.RewardUncommonCardIcon,
                    CardRarity.Rare => ui.RewardRareCardIcon,
                    _ => ui.RewardCommonCardIcon
                };
                if (newIconPath != null)
                {
                    __result = newIconPath;
                }
            }
        }
    }
}