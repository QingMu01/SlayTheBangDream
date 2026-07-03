using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Patches;

public class MusicCardSupportPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<CardNodeSupportMusicTypePatch>();
        patcher.RegisterPatch<AddExtraCardPoolToModelDbPatch>();
        patcher.RegisterPatch<ExtraPoolConcatToStandardPoolInLibraryPatch>();
    }
}

internal class CardNodeSupportMusicTypePatch : IPatchMethod
{
    private static readonly LocString MusicType = new("gameplay_ui", "BANG_DREAM_LIB_MUSIC_TYPE");

    public static string PatchId => "replace_card_type_locString_if_its_implemented_IPerformance";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCard), "UpdateTypePlaque")];
    }

    public static void Postfix(NCard __instance, MegaLabel ____typeLabel)
    {
        if (__instance.Model is IPerformanceCard)
        {
            ____typeLabel.SetTextAutoSize(MusicType.GetFormattedText());
        }
    }
}

internal class AddExtraCardPoolToModelDbPatch : IPatchMethod
{
    public static string PatchId => "add_extra_card_pool_to_model_db";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(ModelDb), nameof(ModelDb.AllCharacterCardPools), MethodType.Getter)];
    }

    public static void Postfix(ref IEnumerable<CardPoolModel> __result)
    {
        var cardPoolModels = __result.ToList();
        foreach (var characterModel in ModelDb.AllCharacters)
        {
            if (characterModel is IExtraDeckSupportCharacter character)
            {
                var characterExtraCardPool = character.ExtraCardPool;
                if (!cardPoolModels.Contains(characterExtraCardPool))
                {
                    cardPoolModels.Add(characterExtraCardPool);
                }
            }
        }

        __result = cardPoolModels;
    }
}

internal class ExtraPoolConcatToStandardPoolInLibraryPatch : IPatchMethod
{
    public static string PatchId => "in_library_only_concat_pool";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCardLibrary), nameof(NCardLibrary._Ready))];
    }

    public static void Postfix(
        Dictionary<NCardPoolFilter, Func<CardModel, bool>> ____poolFilters,
        Dictionary<CharacterModel, NCardPoolFilter> ____cardPoolFilters)
    {
        foreach (var characterModel in ____cardPoolFilters.Keys)
        {
            if (characterModel is IExtraDeckSupportCharacter character)
            {
                var filter = ____cardPoolFilters[characterModel];
                var characterCardPool = characterModel.CardPool;
                var characterExtraCardPool = character.ExtraCardPool;

                ____poolFilters[filter] = model =>
                    characterCardPool.AllCards.Contains(model) || characterExtraCardPool.AllCards.Contains(model);
            }
        }
    }
}