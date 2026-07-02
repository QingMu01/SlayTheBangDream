using System.Reflection;
using BangDreamLib.Scripts.Character;
using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Nodes;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Enums;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;
using STS2RitsuLib.Utils;

namespace BangDreamLib.Scripts.Patches;

public class GroupableCharacterSelectorPatches : IModPatches
{
    internal static readonly FieldInfo CacheInfoPanel =
        AccessTools.Field(typeof(NCharacterSelectScreen), "_infoPanel");

    internal static readonly FieldInfo CacheAscensionPanel =
        AccessTools.Field(typeof(NCharacterSelectScreen), "_ascensionPanel");

    internal static readonly FieldInfo CacheBgContainer =
        AccessTools.Field(typeof(NCharacterSelectScreen), "_bgContainer");

    internal static readonly AttachedState<NCharacterSelectButton, CharacterGroup?> GroupState = new(_ => null);

    internal static BangDreamCharacterSelector? Instance;

    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<HiddenUiPatch>();
        patcher.RegisterPatch<SetupGroupableCharacterSelectorPatch>();
        patcher.RegisterPatch<SetupGroupableCharacterSelectButtonPatch>();
        patcher.RegisterPatch<InitSelectorPatch>();
        patcher.RegisterPatch<SelectGroupButtonIconPatch>();
        patcher.RegisterPatch<InterceptGroupButtonPatch>();
        patcher.RegisterPatch<AscensionPanelHostModePatch>();
        patcher.RegisterPatch<AscensionPanelMultiplayerModePatch>();
        patcher.RegisterPatch<AscensionPanelSingleplayerModePatch>();
    }
}

internal class HiddenUiPatch : IPatchMethod
{
    public static string PatchId => "on_selected_hidden_vanilla_ui";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))];
    }

    public static void Postfix(NCharacterSelectScreen __instance, NCharacterSelectButton charSelectButton)
    {
        var infoPanel = GroupableCharacterSelectorPatches.CacheInfoPanel.GetValue(__instance) as Control;
        var ascensionPanel =
            GroupableCharacterSelectorPatches.CacheAscensionPanel.GetValue(__instance) as NAscensionPanel;
        var bgContainer = GroupableCharacterSelectorPatches.CacheBgContainer.GetValue(__instance) as Control;

        if (infoPanel != null)
        {
            infoPanel.Visible = charSelectButton.Character is not GroupCharacterPlaceholder;
        }

        if (ascensionPanel != null)
        {
            var ascensionVisible = ascensionPanel.Visible;
            ascensionPanel.Visible = charSelectButton.Character is not GroupCharacterPlaceholder && ascensionVisible;
        }

        if (bgContainer != null)
        {
            bgContainer.Visible = charSelectButton.Character is not GroupCharacterPlaceholder;
            if (GroupableCharacterSelectorPatches.Instance != null)
            {
                GroupableCharacterSelectorPatches.Instance.Visible = !bgContainer.Visible;
            }
        }
    }
}

internal class SetupGroupableCharacterSelectorPatch : IPatchMethod
{
    public static string PatchId => "on_selected_setup_custom_ui";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen._Ready))];
    }

    public static void Postfix(NCharacterSelectScreen __instance, Control ____bgContainer)
    {
        var customControl = new Control();
        customControl.Position = new Vector2(____bgContainer.Position.X, ____bgContainer.Position.Y);
        customControl.Size = new Vector2(____bgContainer.Size.X, ____bgContainer.Size.Y);

        __instance.AddChildSafely(customControl);
        var originalIndex = __instance.GetChildren().IndexOf(____bgContainer);
        __instance.MoveChild(customControl, originalIndex + 1);
        var characterSelector = PreloadKey.CharacterSelector.GetScene().Instantiate<BangDreamCharacterSelector>();
        characterSelector.Name = "BangDreamSelector";
        characterSelector.Visible = false;
        customControl.AddChildSafely(characterSelector);

        GroupableCharacterSelectorPatches.Instance = characterSelector;
    }
}

internal class SetupGroupableCharacterSelectButtonPatch : IPatchMethod
{
    public static string PatchId => "on_init_select_screen_setup_group_button";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCharacterSelectScreen), "InitCharacterButtons")];
    }

    // 原版所有角色
    private static readonly List<CharacterModel> VanillaCharacters =
    [
        ModelDb.Character<Ironclad>(),
        ModelDb.Character<Silent>(),
        ModelDb.Character<Regent>(),
        ModelDb.Character<Necrobinder>(),
        ModelDb.Character<Defect>()
    ];

    private static readonly FieldInfo IconField = AccessTools.Field(typeof(NCharacterSelectButton), "_icon");

    [HarmonyPriority(1024)]
    public static void Postfix(NCharacterSelectScreen __instance,
        PackedScene ____charSelectButtonScene, Control ____charButtonContainer)
    {
        SetupButtons(__instance, ____charSelectButtonScene, ____charButtonContainer);

        foreach (var button in ____charButtonContainer.GetChildren().OfType<NCharacterSelectButton>())
        {
            if (button.Character is IGroupableCharacter)
            {
                button.Visible = false;
            }
        }

        var list = ____charButtonContainer.GetChildren().OfType<NCharacterSelectButton>()
            .Where(c => c.Visible)
            .ToList();

        for (var index = 0; index < list.Count; ++index)
        {
            list[index].FocusNeighborTop = list[index].GetPath();
            list[index].FocusNeighborBottom = list[index].GetPath();
            var ncharacterSelectButton = list[index];
            var path = index <= 0 ? list[^1].GetPath() : list[index - 1].GetPath();
            ncharacterSelectButton.FocusNeighborLeft = path;
            list[index].FocusNeighborRight = index < list.Count - 1 ? list[index + 1].GetPath() : list[0].GetPath();
        }
    }

    private static void SetupButtons(NCharacterSelectScreen screen, PackedScene buttonScene, Control container)
    {
        var insertIndex = GetInsertIndex(container.GetChildren().OfType<NCharacterSelectButton>().ToList());

        var effectiveGroups = ModelDb.AllCharacters.OfType<IGroupableCharacter>();
        var alreadyGroupName = new HashSet<string>();

        foreach (var groupableCharacter in effectiveGroups)
        {
            var buttonName = groupableCharacter.Group.GetGroupInfo().Name;
            if (alreadyGroupName.Add(buttonName))
            {
                var child = buttonScene.Instantiate<NCharacterSelectButton>();
                child.Name = buttonName + "_button";
                container.AddChildSafely(child);
                container.MoveChild(child, ++insertIndex);
                child.Init(ModelDb.Character<GroupCharacterPlaceholder>(), screen);
                GroupableCharacterSelectorPatches.GroupState.Set(child, groupableCharacter.Group);

                BangDreamLibCore.Logger.Info($"Add Group Button: {buttonName}");

                var groupSelectIcon = groupableCharacter.Group.GetGroupSelectIcon();
                if (!string.IsNullOrEmpty(groupSelectIcon) && ResourceLoader.Exists(groupSelectIcon))
                {
                    if (IconField.GetValue(child) is TextureRect icon)
                    {
                        icon.Texture = PreloadManager.Cache.GetTexture2D(groupSelectIcon);
                    }
                }
            }
        }
    }

    private static int GetInsertIndex(List<NCharacterSelectButton> buttons)
    {
        var lastCharacterIndex = -1;
        for (var i = buttons.Count - 1; i >= 0; --i)
        {
            var button = buttons[i];
            if (!button.IsLocked && VanillaCharacters.Contains(button.Character))
            {
                lastCharacterIndex = i;
                break;
            }
        }

        return lastCharacterIndex;
    }
}

internal class InitSelectorPatch : IPatchMethod
{
    public static string PatchId => "on_selected_init_content";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.Select))];
    }

    public static void Prefix(NCharacterSelectButton __instance, bool ____isSelected,
        ICharacterSelectButtonDelegate ____delegate, CharacterModel ____character)
    {
        if (!____isSelected && ____delegate is NCharacterSelectScreen && ____character is GroupCharacterPlaceholder)
        {
            var characterGroup = GroupableCharacterSelectorPatches.GroupState.GetOrCreate(__instance);
            if (characterGroup.HasValue)
            {
                GroupableCharacterSelectorPatches.Instance?.Init(characterGroup.Value, __instance, ____delegate);
            }
            else
            {
                throw new NullReferenceException($"Selected group button({__instance.Name}) not be init.");
            }
        }
    }
}

internal class SelectGroupButtonIconPatch : IPatchMethod
{
    public static string PatchId => "select_group_button_icon";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.Init)),
            new ModPatchTarget(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.LockForAnimation)),
            new ModPatchTarget(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.AnimateUnlock)),
            new ModPatchTarget(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.DebugUnlock)),
            new ModPatchTarget(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.UnlockIfPossible))
        ];
    }

    public static void Postfix(NCharacterSelectButton __instance,
        TextureRect ____icon, TextureRect ____lock, TextureRect ____iconAdd)
    {
        if (__instance.Character is GroupCharacterPlaceholder)
        {
            var characterGroup = GroupableCharacterSelectorPatches.GroupState.GetOrCreate(__instance);
            if (characterGroup.HasValue)
            {
                var groupSelectIcon = characterGroup.Value.GetGroupSelectIcon();
                if (!string.IsNullOrEmpty(groupSelectIcon) && ResourceLoader.Exists(groupSelectIcon))
                {
                    var texture = PreloadManager.Cache.GetTexture2D(groupSelectIcon);
                    ____icon.Texture = texture;
                    ____lock.Texture = texture;
                    ____iconAdd.Texture = texture;
                }
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
        return characterModel is not GroupCharacterPlaceholder;
    }
}

internal class AscensionPanelHostModePatch : IPatchMethod
{
    public static string PatchId => "bang_dream_style_ascension_panel_setup_host_mode";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(NCharacterSelectScreen),
                nameof(NCharacterSelectScreen.InitializeMultiplayerAsHost))
        ];
    }


    public static void Postfix(NCharacterSelectScreen __instance, StartRunLobby ____lobby)
    {
        if (GroupableCharacterSelectorPatches.Instance != null)
        {
            GroupableCharacterSelectorPatches.Instance.Lobby = ____lobby;
            GroupableCharacterSelectorPatches.Instance.AscensionPanel.Initialize(MultiplayerUiMode.Host);
        }
    }
}

internal class AscensionPanelMultiplayerModePatch : IPatchMethod
{
    public static string PatchId => "bang_dream_style_ascension_panel_setup_multiplayer_mode";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(NCharacterSelectScreen),
                nameof(NCharacterSelectScreen.InitializeMultiplayerAsClient))
        ];
    }

    public static void Postfix(NCharacterSelectScreen __instance, StartRunLobby ____lobby)
    {
        if (GroupableCharacterSelectorPatches.Instance != null)
        {
            GroupableCharacterSelectorPatches.Instance.Lobby = ____lobby;
            GroupableCharacterSelectorPatches.Instance.AscensionPanel.Initialize(MultiplayerUiMode.Client);
        }
    }
}

internal class AscensionPanelSingleplayerModePatch : IPatchMethod
{
    public static string PatchId => "bang_dream_style_ascension_panel_setup_singleplayer_mode";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.InitializeSingleplayer))
        ];
    }

    public static void Postfix(NCharacterSelectScreen __instance, StartRunLobby ____lobby)
    {
        if (GroupableCharacterSelectorPatches.Instance != null)
        {
            GroupableCharacterSelectorPatches.Instance.Lobby = ____lobby;
            GroupableCharacterSelectorPatches.Instance.AscensionPanel.Initialize(MultiplayerUiMode.Singleplayer);
        }
    }
}