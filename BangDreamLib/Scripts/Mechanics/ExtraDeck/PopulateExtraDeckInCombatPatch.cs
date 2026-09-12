using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Random;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Mechanics.ExtraDeck;

/// <summary>
/// 战斗开始时把额外卡组的卡牌克隆进额外抽牌堆。
/// </summary>
internal class PopulateExtraDeckInCombatPatch : IPatchMethod
{
    public static string PatchId => "on_combat_started_init_extra_draw_pile";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(Player), nameof(Player.PopulateCombatState))];
    }

    public static void Postfix(Player __instance, Rng rng, CombatState state)
    {
        var extraDeck = BangDreamConst.ExtraDeck.GetPile(__instance);
        var extraDraw = BangDreamConst.ExtraDraw.GetPile(__instance);
        foreach (var deckCard in extraDeck.Cards)
        {
            var cloneCard = state.CloneCard(deckCard);
            cloneCard.DeckVersion = deckCard;
            extraDraw.AddInternal(cloneCard);
        }

        extraDraw.RandomizeOrderInternal(__instance, rng, state);
    }
}
