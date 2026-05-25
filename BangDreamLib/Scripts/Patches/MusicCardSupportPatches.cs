using System.Reflection.Emit;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Vfx;
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
        patcher.RegisterPatch<MusicCardRewardSupportPatch>();
        patcher.RegisterPatch<MusicCardRewardVfxSupportPatch>();
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

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        var getModelMethod = AccessTools.PropertyGetter(typeof(NCard), "Model");
        var getTypeMethod = AccessTools.PropertyGetter(typeof(CardModel), "Type");
        var toLocStringMethod = AccessTools.Method(typeof(CardTypeExtensions), nameof(CardTypeExtensions.ToLocString));
        var getMusicTypeMethod = AccessTools.Method(typeof(CardNodeSupportMusicTypePatch), nameof(GetMusicType));

        for (var i = 0; i < codes.Count; i++)
        {
            yield return codes[i];
            if (codes[i].Calls(getModelMethod))
            {
                if (i + 1 < codes.Count && codes[i + 1].Calls(getTypeMethod))
                {
                    yield return new CodeInstruction(OpCodes.Dup);
                }
            }

            if (codes[i].Calls(toLocStringMethod))
            {
                yield return new CodeInstruction(OpCodes.Call, getMusicTypeMethod);
            }
        }
    }

    private static LocString GetMusicType(CardModel card, LocString original)
    {
        return card is IPerformanceCard ? MusicType : original;
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

internal class MusicCardRewardSupportPatch : IPatchMethod
{
    public static string PatchId => "relace_card_reward_added_target_pile";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(CardPileCmd), nameof(CardPileCmd.Add),
            [
                typeof(CardModel),
                typeof(PileType),
                typeof(CardPilePosition),
                typeof(AbstractModel),
                typeof(bool)
            ])
        ];
    }

    public static void Prefix(CardModel card, ref PileType newPileType)
    {
        if (IPerformanceCard.CardEnterExtraDeck.GetOrCreate(card))
        {
            newPileType = BangDreamConst.PileExtraDeck.GetPileType();
        }
    }
}

internal class MusicCardRewardVfxSupportPatch : IPatchMethod
{
    public static string PatchId => "relace_card_reward_added_vfx_target_pile";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCardFlyVfx), nameof(NCardFlyVfx.Create))];
    }

    public static void Prefix(NCard card, ref PileType pileType)
    {
        if (card.Model != null && IPerformanceCard.CardEnterExtraDeck.GetOrCreate(card.Model))
        {
            pileType = BangDreamConst.PileExtraDeck.GetPileType();
        }
    }
}