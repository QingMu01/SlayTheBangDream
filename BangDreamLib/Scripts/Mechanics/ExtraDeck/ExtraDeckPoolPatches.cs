using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace BangDreamLib.Scripts.Mechanics.ExtraDeck;

/// <summary>
/// 把 <see cref="IExtraDeckSupportCharacter" /> 的额外卡池并入角色卡池聚合视图，使其出现在卡库与模型库中。
/// </summary>
public class ExtraDeckPoolPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<AddExtraCardPoolToModelDbPatch>();
        patcher.RegisterPatch<ExtraPoolConcatToStandardPoolInLibraryPatch>();
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
