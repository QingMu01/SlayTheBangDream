using System.Reflection;
using System.Reflection.Emit;
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
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.RestSite;
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

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter._Ready))];
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        for (var i = 0; i < codes.Count - 2; i++)
        {
            if (codes[i].opcode == OpCodes.Ldloc_0 &&
                codes[i + 1].opcode == OpCodes.Ldloc_1 &&
                codes[i + 2].opcode == OpCodes.Stfld &&
                codes[i + 2].operand is FieldInfo { Name: "animName" })
            {
                codes.Insert(i + 2, new CodeInstruction(OpCodes.Ldarg_0));
                codes.Insert(i + 3, CodeInstruction.Call(typeof(NRestSiteCharacter), "get_Player"));
                codes.Insert(i + 4,
                    CodeInstruction.Call(typeof(RestSiteAnimPatch), nameof(GetRestSiteAnimName)));
                break;
            }
        }

        return codes;
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

    private static Type MerchantPatcher =>
        Type.GetType(
            "STS2RitsuLib.Scaffolding.Characters.Patches.NMerchantRoomProceduralCharacterInstantiationPatch,STS2-RitsuLib") ??
        throw new TypeLoadException("RitsuLib Patcher Not Found!");

    private static Type FakeMerchantPatcher =>
        Type.GetType(
            "STS2RitsuLib.Scaffolding.Characters.Patches.NFakeMerchantProceduralCharacterInstantiationPatch,STS2-RitsuLib") ??
        throw new TypeLoadException("RitsuLib Patcher Not Found!");

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(MerchantPatcher, "ApplyMerchantWorldVisuals"),
            new ModPatchTarget(FakeMerchantPatcher, "ApplyMerchantWorldVisuals")
        ];
    }

    public static bool Prefix(IReadOnlyList<Player> players, ref IReadOnlyList<NMerchantCharacter> visuals)
    {
        var n = Math.Min(visuals.Count, players.Count);
        for (var i = 0; i < n; i++)
        {
            var currentPlayer = players[i];
            var oldVisual = visuals[i];
            if (currentPlayer.Character is ISkinSupportCharacter)
            {
                var newVisual = BangDreamMerchant.Create(currentPlayer);
                var parent = oldVisual.GetParent();
                var index = oldVisual.GetIndex();
                parent.AddChild(newVisual);
                parent.MoveChild(newVisual, index);
                oldVisual.QueueFreeSafely();
                continue;
            }

            oldVisual.PlayAnimation("relaxed_loop", true);
        }

        return false;
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