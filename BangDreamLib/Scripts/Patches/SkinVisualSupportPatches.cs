using System.Reflection;
using System.Reflection.Emit;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Nodes.MegeScript;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.TreasureRelicPicking;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;
using STS2RitsuLib.Scaffolding.Godot;

namespace BangDreamLib.Scripts.Patches;

public class SkinVisualSupportPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<VisualPatch>();
        patcher.RegisterPatch<RestSiteScenePatch>();
        patcher.RegisterPatch<RestSiteAnimPatch>();
        patcher.RegisterPatch<MerchantScenePatch>();
        patcher.RegisterPatch<ArmPointingTexturePatch>();
        patcher.RegisterPatch<ArmFightTexturePatch>();
    }
}

internal class VisualPatch : IPatchMethod
{
    public static string PatchId => "on_create_visual_replace_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCreature), nameof(NCreature.Create))];
    }

    public static void Postfix(ref NCreature? __result)
    {
        if (__result?.Entity.Player?.Character is ISkinSupportCharacter)
        {
            var path = __result.Entity.Player.AttachedData().SkinManager.CurrentSkin?.SkinTemplate.MultiplayerVisual
                .VisualScene;
            if (path != null)
            {
                var nCreatureVisuals =
                    RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(path);
                Traverse.Create(__result).Property("Visuals").SetValue(nCreatureVisuals);
            }
        }
    }
}

internal class RestSiteScenePatch : IPatchMethod
{
    public static string PatchId => "on_create_rest_site_replace_by_skin_info";

    private static readonly PropertyInfo CachePlayer = AccessTools.Property(typeof(NRestSiteCharacter), "Player");
    private static readonly FieldInfo CacheIndex = AccessTools.Field(typeof(NRestSiteCharacter), "_characterIndex");

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter.Create))];
    }

    public static void Postfix(ref NRestSiteCharacter? __result, Player player, int characterIndex)
    {
        if (player.Character is ISkinSupportCharacter)
        {
            var path = player.AttachedData().SkinManager.CurrentSkin?.SkinTemplate.MultiplayerVisual.RestSiteScene;
            if (path != null)
            {
                var nRestSiteCharacter = RitsuGodotNodeFactories.CreateFromScenePath<NRestSiteCharacter>(path);

                CachePlayer.SetValue(nRestSiteCharacter, player);
                CacheIndex.SetValue(nRestSiteCharacter, characterIndex);
                __result = nRestSiteCharacter;
            }
        }
    }
}

internal class RestSiteAnimPatch : IPatchMethod
{
    public static string PatchId => "on_create_rest_site_anim_replace_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter._Ready))];
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        for (var i = 0; i < codes.Count - 1; i++)
        {
            if (codes[i].opcode == OpCodes.Ldloc_1 && codes[i + 1].opcode == OpCodes.Stloc_0)
            {
                var injected = new List<CodeInstruction>
                {
                    new(OpCodes.Ldloc_0),
                    new(OpCodes.Ldarg_0),
                    CodeInstruction.Call(typeof(NRestSiteCharacter), "get_Player"),
                    CodeInstruction.Call(typeof(RestSiteAnimPatch), nameof(GetRestSiteAnimName)),
                    new(OpCodes.Stloc_0)
                };
                codes.InsertRange(i + 2, injected);
                break;
            }
        }

        return codes;
    }

    private static string GetRestSiteAnimName(string originalName, Player player)
    {
        if (player.Character is ISkinSupportCharacter)
        {
            return player.AttachedData().SkinManager.CurrentSkin?.SkinTemplate.MultiplayerVisual.RestSiteAnimName ??
                   originalName;
        }

        return originalName;
    }
}

internal class MerchantScenePatch : IPatchMethod
{
    public static string PatchId => "on_create_merchant_replace_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NMerchantRoom), "AfterRoomIsLoaded")];
    }

    [HarmonyPriority(1024)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var targetInstantiate = typeof(PackedScene)
            .GetMethods()
            .First(m => m is { Name: "Instantiate", IsGenericMethod: true })
            .MakeGenericMethod(typeof(NMerchantCharacter));
        var createMethod = AccessTools.Method(typeof(BangDreamMerchant), nameof(BangDreamMerchant.Create));
        var playersField = AccessTools.Field(typeof(NMerchantRoom), "_players");
        var listItemGetter = AccessTools.Method(typeof(List<Player>), "get_Item");

        var startIndex = -1;
        var endIndex = -1;

        for (var i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(targetInstantiate))
            {
                endIndex = i;
                for (var j = i; j >= 0; j--)
                {
                    if (codes[j].opcode == OpCodes.Call &&
                        codes[j].operand is MethodInfo { Name: "get_Cache" })
                    {
                        startIndex = j;
                        break;
                    }
                }

                break;
            }
        }

        if (startIndex != -1 && endIndex != -1)
        {
            codes.RemoveRange(startIndex, endIndex - startIndex + 1);

            var newInstructions = new List<CodeInstruction>
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, playersField),
                new(OpCodes.Ldloc_S, 5),
                new(OpCodes.Callvirt, listItemGetter),
                new(OpCodes.Call, createMethod)
            };

            codes.InsertRange(startIndex, newInstructions);
        }

        return codes.AsEnumerable();
    }
}

internal class ArmPointingTexturePatch : IPatchMethod
{
    public static string PatchId => "on_create_arm_pointing_replace_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NHandImage), nameof(NHandImage._Ready))];
    }

    public static void Postfix(NHandImage __instance, TextureRect ____textureRect)
    {
        if (__instance.Player.Character is ISkinSupportCharacter)
        {
            var path = __instance.Player.AttachedData().SkinManager.CurrentSkin?.SkinTemplate.MultiplayerVisual
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
    public static string PatchId => "on_create_fight_arm_replace_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NHandImage), "SetTextureToFightMove")];
    }

    public static void Postfix(NHandImage __instance, RelicPickingFightMove move, TextureRect ____textureRect)
    {
        if (__instance.Player.Character is ISkinSupportCharacter)
        {
            var armTextureSet = __instance.Player.AttachedData().SkinManager.CurrentSkin?.SkinTemplate
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