using System.Reflection;
using BangDreamLib.Scripts.Character;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Nodes;
using BangDreamLib.Scripts.Utils;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;
using STS2RitsuLib.Utils;

namespace BangDreamLib.Scripts.Patches;

public class AggregationSelectorPatches : IModPatches
{
    internal static readonly FieldInfo CacheInfoPanel =
        AccessTools.Field(typeof(NCharacterSelectScreen), "_infoPanel");

    internal static readonly FieldInfo CacheAscensionPanel =
        AccessTools.Field(typeof(NCharacterSelectScreen), "_ascensionPanel");

    internal static readonly FieldInfo CacheBgContainer =
        AccessTools.Field(typeof(NCharacterSelectScreen), "_bgContainer");

    internal static readonly AttachedState<NCharacterSelectScreen, NCharacterSelector> Selector = new();

    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<HiddenUiPatch>();
        patcher.RegisterPatch<HiddenEmptyMemberGroupPatch>();
        patcher.RegisterPatch<SetupAggregationSelectorPatch>();
        patcher.RegisterPatch<InitSelectorPatch>();
        patcher.RegisterPatch<InterceptGroupButtonPatch>();
    }
}

internal class HiddenUiPatch : IPatchMethod
{
    public static string PatchId => "on_selected_aggregation_hidden_original_ui";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCharacterSelectScreen), "SelectCharacter")];
    }

    public static void Postfix(NCharacterSelectScreen __instance, NCharacterSelectButton charSelectButton,
        CharacterModel characterModel)
    {
        var infoPanel = AggregationSelectorPatches.CacheInfoPanel.GetValue(__instance) as Control;
        var ascensionPanel = AggregationSelectorPatches.CacheAscensionPanel.GetValue(__instance) as NAscensionPanel;
        var bgContainer = AggregationSelectorPatches.CacheBgContainer.GetValue(__instance) as Control;

        if (infoPanel != null)
        {
            infoPanel.Visible = charSelectButton.Character is not IAggregationGroup;
        }

        if (ascensionPanel != null)
        {
            var ascensionVisible = ascensionPanel.Visible;
            ascensionPanel.Visible = charSelectButton.Character is not IAggregationGroup && ascensionVisible;
        }

        if (bgContainer != null)
        {
            bgContainer.Visible = charSelectButton.Character is not IAggregationGroup;
            if (AggregationSelectorPatches.Selector.ContainsKey(__instance))
            {
                AggregationSelectorPatches.Selector[__instance].Visible = !bgContainer.Visible;
            }
        }
    }
}

internal class HiddenEmptyMemberGroupPatch : IPatchMethod
{
    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.Init))];
    }

    public static string PatchId => "on_init_character_select_button_hidden_empty_group_button";

    public static void Postfix(NCharacterSelectButton __instance, CharacterModel character)
    {
        if (character is BangDreamGroup group)
        {
            __instance.Visible = group.HasEffectiveMember;
            if (__instance.Visible)
            {
                BangDreamLibCore.Logger.Info($"Band {group.Band} is Ready!");
            }
        }
    }
}

internal class SetupAggregationSelectorPatch : IPatchMethod
{
    public static string PatchId => "on_selected_aggregation_setup_custom_ui";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen._Ready))];
    }

    public static void Postfix(NCharacterSelectScreen __instance, Control ____bgContainer)
    {
        var duplicate = new Control();
        duplicate.Position = new Vector2(____bgContainer.Position.X, ____bgContainer.Position.Y);
        duplicate.Size = new Vector2(____bgContainer.Size.X, ____bgContainer.Size.Y);

        __instance.AddChildSafely(duplicate);
        var originalIndex = __instance.GetChildren().IndexOf(____bgContainer);
        __instance.MoveChild(duplicate, originalIndex + 1);
        var characterSelector = PreloadKey.CharacterSelector.GetScene().Instantiate<NCharacterSelector>();
        characterSelector.Name = "BangDreamSelector";
        characterSelector.Visible = false;
        duplicate.AddChildSafely(characterSelector);

        AggregationSelectorPatches.Selector.Set(__instance, characterSelector);
    }
}

internal class InitSelectorPatch : IPatchMethod
{
    public static string PatchId => "on_selected_aggregation_button_init_content";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.Select))];
    }

    public static void Prefix(NCharacterSelectButton __instance, bool ____isSelected,
        ICharacterSelectButtonDelegate ____delegate, CharacterModel ____character)
    {
        if (!____isSelected && ____delegate is NCharacterSelectScreen screen &&
            ____character is IAggregationGroup group)
        {
            if (AggregationSelectorPatches.Selector.ContainsKey(screen))
            {
                AggregationSelectorPatches.Selector[screen].Init(group, __instance, ____delegate);
            }
            else
            {
                SetupAggregationSelectorPatch.Postfix(screen, screen.GetNode<Control>((NodePath)"AnimatedBg"));
                AggregationSelectorPatches.Selector[screen].Init(group, __instance, ____delegate);
            }
        }
    }
}

internal class InterceptGroupButtonPatch : IPatchMethod
{
    public static string PatchId => "stop_group_spread";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))];
    }

    public static bool Prefix(NCharacterSelectScreen __instance, NCharacterSelectButton charSelectButton,
        ref CharacterModel characterModel)
    {
        return characterModel is not IAggregationGroup;
    }
}