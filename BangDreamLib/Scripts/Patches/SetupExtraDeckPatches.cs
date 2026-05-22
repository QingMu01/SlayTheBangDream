using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Relics.GameRules;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Patches;

public class SetupExtraDeckPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<InitStartingExtraDeckPatch>();
        patcher.RegisterPatch<LoadExtraDeckPatch>();
    }
}

internal class InitStartingExtraDeckPatch : IPatchMethod
{
    public static string PatchId => "on_create_new_run_add_extra_deck_to_run_state";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(RunState), "CreateShared")];
    }

    public static void Postfix(RunState __result)
    {
        foreach (var player in __result.Players)
        {
            var cardPile = BangDreamConst.PileExtraDeck.GetPile(player);
            foreach (var card in cardPile.Cards)
            {
                __result.AddCard(card, player);
            }
        }
    }
}

internal class LoadExtraDeckPatch : IPatchMethod
{
    public static string PatchId => "load_extra_deck_from_hidden_relic";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(Player), "LoadInventory")];
    }

    public static void Postfix(Player __instance)
    {
        var extraDeckHelper = __instance.GetRelic<ExtraDeckSaveHelper>();
        if (extraDeckHelper != null)
        {
            var extraDeck = BangDreamConst.PileExtraDeck.GetPile(__instance);
            extraDeck.Clear(true);
            foreach (var serializableCard in extraDeckHelper.ExtraCards)
            {
                extraDeck.AddInternal(CardModel.FromSerializable(serializableCard), silent: true);
            }
        }
    }
}