using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Relics.GameRules;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Patches;

public class SkinContentSupportPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<InitDeckPatch>();
        patcher.RegisterPatch<InitExtraDeckPatch>();
        patcher.RegisterPatch<InitRelicPatch>();
        patcher.RegisterPatch<InitPotionPatch>();
    }
}

internal class InitDeckPatch : IPatchMethod
{
    public static string PatchId => "on_init_deck_replace_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(Player), "PopulateStartingDeck")];
    }

    public static bool Prefix(Player __instance)
    {
        if (__instance.Character is ISkinSupportCharacter)
        {
            var skinInfo = __instance.AttachedData().SkinManager.CurrentSkin;
            if (skinInfo != null)
            {
                var startingDeck = new List<CardModel>();
                foreach (var cardModel in skinInfo.GetStartingDeck())
                {
                    var mutable = cardModel.ToMutable();
                    mutable.FloorAddedToDeck = 1;
                    startingDeck.Add(mutable);
                }

                if (startingDeck.Count > 0)
                {
                    if (__instance.Deck.Cards.Any())
                        throw new InvalidOperationException("Deck has already been populated.");
                    foreach (var card in startingDeck)
                        __instance.Deck.AddInternal(card, silent: false);
                    BangDreamLibCore.Logger.Debug(
                        $"use SkinInfo.Starting.Deck({startingDeck.Count}) replace character {__instance.Character} starting deck");
                    return false;
                }
            }
        }

        return true;
    }
}

internal class InitExtraDeckPatch : IPatchMethod
{
    public static string PatchId => "on_init_set_extra_deck";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(Player), "PopulateStartingInventory")];
    }

    public static void Postfix(Player __instance)
    {
        if (__instance.Character is ISkinSupportCharacter)
        {
            var skinInfo = __instance.AttachedData().SkinManager.CurrentSkin;
            if (skinInfo != null)
            {
                var extraDeck = new List<CardModel>();
                foreach (var cardModel in skinInfo.GetStartingExtraDeck())
                {
                    var mutable = cardModel.ToMutable();
                    mutable.FloorAddedToDeck = 1;
                    extraDeck.Add(mutable);
                }

                if (extraDeck.Count > 0)
                {
                    PopulateDeck(extraDeck, __instance);
                    var extraDeckSaveHelper = ModelDb.Relic<ExtraDeckSaveHelper>().ToMutable();
                    extraDeckSaveHelper.FloorAddedToDeck = 1;
                    __instance.AddRelicInternal(extraDeckSaveHelper, silent: true);
                    BangDreamLibCore.Logger.Debug(
                        $"Init character {__instance.Character} Extra Deck({extraDeck.Count}) by SkinInfo.Starting.ExtraDeck");
                }
            }
        }
    }

    private static void PopulateDeck(IEnumerable<CardModel> cards, Player player, bool silent = false)
    {
        var extraDeck = BangDreamTools.GetPile(BangDreamConst.ExtraDeck, player);
        if (extraDeck.Cards.Any())
            throw new InvalidOperationException("Extra Deck has already been populated.");

        foreach (var card in cards)
            extraDeck.AddInternal(card, silent: silent);
    }
}

internal class InitRelicPatch : IPatchMethod
{
    public static string PatchId => "on_init_relics_replace_by_skin_info";


    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(Player), "PopulateStartingRelics")];
    }

    public static bool Prefix(Player __instance)
    {
        if (__instance.Character is ISkinSupportCharacter)
        {
            var skinInfo = __instance.AttachedData().SkinManager.CurrentSkin;
            if (skinInfo != null)
            {
                var startingRelics = new List<RelicModel>();
                foreach (var relicModel in skinInfo.GetStartingRelics())
                {
                    var mutable = relicModel.ToMutable();
                    mutable.FloorAddedToDeck = 1;
                    startingRelics.Add(mutable);
                }

                if (startingRelics.Count > 0)
                {
                    if (__instance.Relics.Any())
                        throw new InvalidOperationException("Relics have already been populated.");
                    foreach (var relic in startingRelics)
                        __instance.AddRelicInternal(relic, silent: false);
                    BangDreamLibCore.Logger.Debug(
                        $"use SkinInfo.Starting.Relics({startingRelics.Count}) replace character {__instance.Character} starting relic.");
                    return false;
                }
            }
        }

        return true;
    }
}

internal class InitPotionPatch : IPatchMethod
{
    public static string PatchId => "on_init_potions_replace_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(Player), "PopulateStartingInventory")];
    }

    // 顺手的事，随便实现一下
    public static void Postfix(Player __instance)
    {
        if (__instance.Character is ISkinSupportCharacter)
        {
            var skinInfo = __instance.AttachedData().SkinManager.CurrentSkin;
            if (skinInfo != null)
            {
                foreach (var startingPotion in skinInfo.GetStartingPotions())
                {
                    __instance.AddPotionInternal(startingPotion.ToMutable());
                }
            }
        }
    }
}