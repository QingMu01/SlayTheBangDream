using System.Reflection;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Nodes.MegeScript;
using BangDreamLib.Scripts.Utils;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.TreasureRelicPicking;
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

namespace BangDreamLib.Scripts.Patches;

public class SkinVisualSupportPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<VisualPatch>();
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

internal class VisualPatch : IPatchMethod
{
    public static string PatchId => "create_visual_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCreature), nameof(NCreature.Create))];
    }

    public static void Postfix(ref NCreature? __result)
    {
        if (__result?.Entity.Player?.Character is ISkinSupportCharacter)
        {
            var path = BangDreamConst.PlayerSkin.Get(__result.Entity.Player).GetSkin()?.SkinTemplate.MultiplayerVisual
                .VisualScene;
            if (path != null)
            {
                var visuals = RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(path);
                Traverse.Create(__result).Property("Visuals").SetValue(visuals);
            }
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
            var path = BangDreamConst.PlayerSkin.Get(player).GetSkin()?.SkinTemplate.Ui
                .EnergyCounterScene;
            if (path != null)
            {
                var energyCounter = RitsuGodotNodeFactories.CreateFromScenePath<NEnergyCounter>(path);
                Traverse.Create(energyCounter).Field("_player").SetValue(player);
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
            var path = BangDreamConst.PlayerSkin.Get(player).GetSkin()?.SkinTemplate.MultiplayerVisual.RestSiteScene;
            if (path != null)
            {
                var restSiteCharacter = RitsuGodotNodeFactories.CreateFromScenePath<NRestSiteCharacter>(path);

                CachePlayer.SetValue(restSiteCharacter, player);
                CacheIndex.SetValue(restSiteCharacter, characterIndex);
                __result = restSiteCharacter;
            }
        }
    }
}

internal class RestSiteAnimPatch : IPatchMethod
{
    public static string PatchId => "create_rest_site_anim_by_skin_info";

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

        var pattern = HarmonyIlPattern.Sequence(
            HarmonyIl.IsLdloc(0),
            HarmonyIl.IsLdloc(1),
            HarmonyIl.IsStfld()
        );

        var matches = pattern.FindAll(rewriter.Code);

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

        BangDreamLibCore.Logger.Error(
            $"Replace rest site animName transpiler failed to match. found {matches.Count} il codes.");
        return rewriter.Code;
    }

    private static string GetRestSiteAnimName(string originalName, Player player)
    {
        if (player.Character is ISkinSupportCharacter)
        {
            return BangDreamConst.PlayerSkin.Get(player).GetSkin()?.SkinTemplate.MultiplayerVisual.RestSiteAnimName ??
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
        for (var i = 0; i < players.Count; i++)
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
        var nodeContainer = ____characterContainer.GetChildren().OfType<NMerchantCharacter>().ToList();
        for (var i = 0; i < players.Count; i++)
        {
            var currentPlayer = players[i];
            var merchantCharacter = BangDreamMerchant.Create(currentPlayer);
            if (merchantCharacter != null)
            {
                var currentNode = nodeContainer[i];
                var index = currentNode.GetIndex();
                ____characterContainer.AddChild(merchantCharacter);
                ____characterContainer.MoveChild(merchantCharacter, index);

                currentNode.QueueFree();
            }
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
            var path = BangDreamConst.PlayerSkin.Get(__instance.Player).GetSkin()?.SkinTemplate.MultiplayerVisual
                .ArmPointingTexture;
            if (path != null)
            {
                ____textureRect.Texture = PreloadManager.Cache.GetTexture2D(path);
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
            var armTextureSet = BangDreamConst.PlayerSkin.Get(__instance.Player).GetSkin()?.SkinTemplate
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
                ____textureRect.Texture = PreloadManager.Cache.GetTexture2D(path);
            }
        }
    }
}

internal class MusicCardFramePatch : IPatchMethod
{
    public static string PatchId => "create_music_card_frame_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCard), "UpdatePortrait"),];
    }

    public static void Postfix(NCard __instance, ref TextureRect? ____frame)
    {
        if (__instance.Model is IPerformCard && __instance.Model is
                { IsMutable: true, Owner.Character: ISkinSupportCharacter })
        {
            var path = BangDreamConst.PlayerSkin.Get(__instance.Model.Owner).GetSkin()?.SkinTemplate.Ui.MusicCardFrame;
            if (path != null)
            {
                ____frame ??= new TextureRect();
                ____frame.Texture = PreloadManager.Cache.GetTexture2D(path);
            }
        }
    }
}

internal class MusicCardFrameMaterialPatch : IPatchMethod
{
    public static string PatchId => "clear_music_card_frame_material_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCard), "Reload"),];
    }

    public static void Postfix(NCard __instance, ref TextureRect? ____frame)
    {
        if (__instance.Model is IPerformCard && __instance.Model is
                { IsMutable: true, Owner.Character: ISkinSupportCharacter })
        {
            var path = BangDreamConst.PlayerSkin.Get(__instance.Model.Owner).GetSkin()?.SkinTemplate.Ui.MusicCardFrame;
            if (path != null)
            {
                if (____frame != null) ____frame.Material = null;
            }
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
            var path = BangDreamConst.PlayerSkin.Get(player).GetSkin()?.SkinTemplate.Ui.MapMarker;
            if (path != null)
            {
                __instance.Texture = PreloadManager.Cache.GetTexture2D(path);
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
        if (player?.Character is ISkinSupportCharacter)
        {
            var ui = BangDreamConst.PlayerSkin.Get(player).GetSkin()?.SkinTemplate.Ui;
            if (ui != null)
            {
                var hpIconPath = ui.TopBarHpIcon;
                var goldIconPath = ui.TopBarGoldIcon;
                var floorIconPath = ui.TopBarFloorIcon;
                var mapIconPath = ui.TopBarMapIcon;
                var deckIconPath = ui.TopBarDeckIcon;
                var settingIconPath = ui.TopBarSettingIcon;

                if (hpIconPath != null)
                {
                    var textureRect = __instance.Hp.GetNodeOrNull<TextureRect>("HpIcon");
                    if (textureRect != null)
                        textureRect.Texture = PreloadManager.Cache.GetTexture2D(hpIconPath);
                }

                if (goldIconPath != null)
                {
                    var textureRect = __instance.Gold.GetNodeOrNull<TextureRect>("GoldIcon");
                    if (textureRect != null)
                        textureRect.Texture = PreloadManager.Cache.GetTexture2D(goldIconPath);
                }

                if (floorIconPath != null)
                {
                    var textureRect =
                        __instance.FloorIcon.GetNodeOrNull<TextureRect>("FloorIconPositioner/FloorInfoIcon");
                    if (textureRect != null)
                        textureRect.Texture = PreloadManager.Cache.GetTexture2D(floorIconPath);
                }

                if (mapIconPath != null)
                {
                    var textureRect = __instance.Map.GetNodeOrNull<TextureRect>("Control/Icon");
                    if (textureRect != null)
                        textureRect.Texture = PreloadManager.Cache.GetTexture2D(mapIconPath);
                }

                if (deckIconPath != null)
                {
                    var textureRect = __instance.Deck.GetNodeOrNull<TextureRect>("Control/Icon");
                    if (textureRect != null)
                        textureRect.Texture = PreloadManager.Cache.GetTexture2D(deckIconPath);
                }

                if (settingIconPath != null)
                {
                    var textureRect = __instance.Pause.GetNodeOrNull<TextureRect>("Control/Icon");
                    if (textureRect != null)
                        textureRect.Texture = PreloadManager.Cache.GetTexture2D(settingIconPath);
                }
            }
        }
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
        if (__instance.Definition is { Id: "BANG_DREAM_LIB_CARDPILE_EXTRA_DECK", Style: ModCardPileUiStyle.TopBarDeck }
            && LocalContext.IsMe(player) && player.Character is ISkinSupportCharacter)
        {
            var path = BangDreamConst.PlayerSkin.Get(player).GetSkin()?.SkinTemplate.Ui.TopBarExtraDeckIcon;
            if (path != null && ____icon is TextureRect textureRect)
            {
                textureRect.Texture = PreloadManager.Cache.GetTexture2D(path);
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
                var path = BangDreamConst.PlayerSkin.Get(player).GetSkin()?.SkinTemplate.Ui.CombatDrawIcon;
                if (path != null && ____icon is TextureRect textureRect)
                {
                    textureRect.Texture = PreloadManager.Cache.GetTexture2D(path);
                }
            }
            else if (__instance is NDiscardPileButton)
            {
                var path = BangDreamConst.PlayerSkin.Get(player).GetSkin()?.SkinTemplate.Ui.CombatDiscardIcon;
                if (path != null && ____icon is TextureRect textureRect)
                {
                    textureRect.Texture = PreloadManager.Cache.GetTexture2D(path);
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
            var ui = BangDreamConst.PlayerSkin.Get(__instance.Player).GetSkin()?.SkinTemplate.Ui;
            if (ui != null)
            {
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