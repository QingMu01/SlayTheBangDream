using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Random;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Patches;

public class PopulateExtraDeckInCombatPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<PopulateExtraDeckInCombatPatch>();
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
        var extraDeck = BangDreamConst.PileExtraDeck.GetPile(__instance);
        var extraDraw = BangDreamConst.PileExtraDraw.GetPile(__instance);
        foreach (var deckCard in extraDeck.Cards)
        {
            var cloneCard = state.CloneCard(deckCard);
            cloneCard.DeckVersion = deckCard;
            extraDraw.AddInternal(cloneCard);
        }

        extraDraw.RandomizeOrderInternal(__instance, rng, state);
    }
}