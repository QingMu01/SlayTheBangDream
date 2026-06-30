using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Patches;

public class SkinStartingContentPopulatePatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<DisableVanillaStartingPopulate>();
        patcher.RegisterPatch<PopulateExtraDeckInCombatPatch>();
        patcher.RegisterPatch<PopulateStartingContentPatch>();
    }
}

internal class DisableVanillaStartingPopulate : IPatchMethod
{
    public static string PatchId => "disable_vanilla_populate";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(Player), "PopulateStartingInventory"),
            new ModPatchTarget(typeof(Player), "PopulateStartingDeck"),
            new ModPatchTarget(typeof(Player), "PopulateStartingRelics")
        ];
    }

    public static bool Prefix(Player __instance)
    {
        return __instance.Character is not ISkinSupportCharacter;
    }
}

internal class PopulateExtraDeckInCombatPatch : IPatchMethod
{
    public static string PatchId => "on_combat_started_init_extra_draw_pile";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(Player), nameof(Player.PopulateCombatState))];
    }

    public static void Postfix(Player __instance, Rng rng, CombatState state)
    {
        var extraDeck = BangDreamTools.GetPile(BangDreamConst.ExtraDeck, __instance);
        var extraDraw = BangDreamTools.GetPile(BangDreamConst.ExtraDraw, __instance);
        foreach (var deckCard in extraDeck.Cards)
        {
            var cloneCard = state.CloneCard(deckCard);
            cloneCard.DeckVersion = deckCard;
            extraDraw.AddInternal(cloneCard);
        }

        extraDraw.RandomizeOrderInternal(__instance, rng, state);
    }
}

internal class PopulateStartingContentPatch : IPatchMethod
{
    public static string PatchId => "populate_starting_content_by_skin_info";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NGame), "StartRun")];
    }

    public static void Prefix(RunState runState)
    {
        foreach (var player in runState.Players)
        {
            if (player.Character is ISkinSupportCharacter)
            {
                var playerSkinData = BangDreamConst.PlayerSkin.Get(player);
                BangDreamLibCore.Logger.Info($"Player {player.NetId} populate starting content.");
                var skinInfo = playerSkinData.GetSkin();
                if (skinInfo != null)
                {
                    PopulateDeck(player, runState, skinInfo.GetStartingDeck());
                    PopulateExtraDeck(player, runState, skinInfo.GetStartingExtraDeck());
                    PopulateRelics(player, skinInfo.GetStartingRelics());
                    PopulatePotions(player, skinInfo.GetStartingPotions());
                    BangDreamLibCore.Logger.Info($"Player {player.Character} populate starting content done.");
                }
            }
            else
            {
                BangDreamLibCore.Logger.Info($"Player {player.Character} is don't support skin, skip populate.");
            }
        }
    }

    private static void PopulateDeck(Player player, RunState runState, IEnumerable<CardModel> cards)
    {
        if (player.Deck.Cards.Any())
            throw new InvalidOperationException("Deck has already been populated.");
        var startingDeck = new List<CardModel>();
        foreach (var cardModel in cards)
        {
            var mutable = cardModel.ToMutable();
            mutable.FloorAddedToDeck = 1;
            startingDeck.Add(mutable);
        }

        if (startingDeck.Count > 0)
        {
            foreach (var card in startingDeck)
            {
                player.Deck.AddInternal(card, silent: false);
                runState.AddCard(card, player);
            }
        }
        else
        {
            BangDreamLibCore.Logger.Error($"ISkinSupportCharacter({player.Character}) has empty Deck.");
        }
    }

    private static void PopulateExtraDeck(Player player, RunState runState, IEnumerable<CardModel> cards)
    {
        var cardPile = BangDreamConst.ExtraDeck.GetPile(player);
        if (cardPile.Cards.Any())
            throw new InvalidOperationException("ExtraDeck has already been populated.");
        var startingExtraDeck = new List<CardModel>();
        foreach (var cardModel in cards)
        {
            var mutable = cardModel.ToMutable();
            mutable.FloorAddedToDeck = 1;
            startingExtraDeck.Add(mutable);
            if (startingExtraDeck.Count > 0)
            {
                foreach (var card in startingExtraDeck)
                {
                    cardPile.AddInternal(card, silent: false);
                    runState.AddCard(card, player);
                }

                // var extraDeckSaveHelper = ModelDb.Relic<ExtraDeckSaveHelper>().ToMutable();
                // extraDeckSaveHelper.FloorAddedToDeck = 1;
                // player.AddRelicInternal(extraDeckSaveHelper, silent: true);
            }
            else
            {
                BangDreamLibCore.Logger.Info(
                    $"ISkinSupportCharacter({player.Character}) has empty ExtraDeck(optional).");
            }
        }
    }

    private static void PopulateRelics(Player player, IEnumerable<RelicModel> relics)
    {
        if (player.Relics.Any())
            throw new InvalidOperationException("Relics has already been populated.");
        foreach (var relicModel in relics)
        {
            var mutable = relicModel.ToMutable();
            mutable.FloorAddedToDeck = 1;
            player.AddRelicInternal(mutable, silent: false);
        }
    }

    private static void PopulatePotions(Player player, IEnumerable<PotionModel> potions)
    {
        if (player.Potions.Any())
            throw new InvalidOperationException("Potions has already been populated.");
        foreach (var potionModel in potions)
        {
            player.AddPotionInternal(potionModel.ToMutable(), silent: false);
        }
    }
}