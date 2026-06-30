using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Cards.DynamicVars;

namespace BangDreamLib.Scripts.Patches.Runtime;

public class AutoSetSubsideVarPatch
{
    public static void Postfix(ISubsideCard __instance, ref IEnumerable<DynamicVar> __result)
    {
        if (__instance is CardModel)
        {
            var dynamicVars = __result.ToList();
            dynamicVars.Add(new SubsideVar(__instance.LingeredEnergyCost));
            __result = dynamicVars;
        }
    }

    private class SubsideVar : DynamicVar
    {
        private const string DefaultName = "Subside";

        internal SubsideVar(decimal baseValue) : base(DefaultName, baseValue)
        {
            this.WithSharedTooltip("BANG_DREAM_LIB_SUBSIDE");
        }

        public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target,
            bool runGlobalHooks)
        {
            var origBaseValue = BaseValue;
            if (card.CombatState == null)
            {
                PreviewValue = origBaseValue;
                return;
            }

            PreviewValue = BangDreamHook.ModifyLingeredEnergyReduce(card.CombatState, origBaseValue);
        }
    }
}