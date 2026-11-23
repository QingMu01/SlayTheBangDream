using System.Reflection;
using System.Runtime.CompilerServices;
using BangDreamInSpire.Scripts.Character;
using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Nodes;
using BangDreamInSpire.Scripts.Nodes.GroupSelect;
using BangDreamInSpire.Scripts.Utils;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Blitz;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.Dread;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;
using STS2RitsuLib.Utils;

namespace BangDreamInSpire.Scripts.Patches;

public class GroupableCharacterSelectorPatches : IModPatches
{
    internal static readonly AttachedState<NCharacterSelectButton, CharacterGroup?> GroupState = new(_ => null);

    /// <summary>每个屏幕独立持有选择器；多个屏会在启动时同时 _Ready，静态共享会串屏。</summary>
    private static readonly ConditionalWeakTable<object, BangDreamCharacterSelector> Selectors = new();

    internal static BangDreamCharacterSelector? GetSelector(object? screen) =>
        screen != null && Selectors.TryGetValue(screen, out var selector) ? selector : null;

    internal static void SetSelector(object screen, BangDreamCharacterSelector selector) =>
        Selectors.AddOrUpdate(screen, selector);

    /// <summary>通过真实角色的隐藏按钮执行原生提交流程；后续逻辑读取
    /// _selectedButton.Character，不能使用分组占位按钮。</summary>
    internal static bool CommitCharacterSelection(object screen, CharacterModel character,
        NCharacterSelectButton? sourceButton)
    {
        var container = CharacterSelectScreenSupport.GetCharButtonContainer(screen);
        if (container == null)
        {
            BangDreamInSpireCore.Logger.Warn(
                $"[GroupSelect] {screen.GetType().Name} 上找不到 _charButtonContainer，无法提交角色选择。");
            return false;
        }

        var target = container.GetChildren().OfType<NCharacterSelectButton>()
            .FirstOrDefault(button => ReferenceEquals(button.Character, character));
        if (target == null)
        {
            BangDreamInSpireCore.Logger.Warn(
                $"[GroupSelect] 找不到角色 {character.Id.Entry} 的原生按钮，无法提交选择。");
            return false;
        }

        target.Select();

        if (sourceButton != null && GodotObject.IsInstanceValid(sourceButton))
        {
            // 恢复可见分组按钮的选中态，并抑制 Init 重置选择器。
            InitSelectorPatch.BeginSuppressInit();
            try
            {
                sourceButton.Select();
            }
            finally
            {
                InitSelectorPatch.EndSuppressInit();
            }

            if (sourceButton.IsInsideTree())
            {
                sourceButton.TryGrabFocus();
            }
        }

        // 原生流程会把本体信息面板/背景显示出来并隐藏自定义选择器，这里切回分组视图。
        ApplyGroupView(screen);
        return true;
    }

    /// <summary>切回「分组选择器」视图（各屏差异由 <see cref="CharacterSelectAdapter" /> 处理）。</summary>
    internal static void ApplyGroupView(object screen) => CharacterSelectAdapter.Apply(screen, groupView: true);

    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<HiddenUiPatch>();
        patcher.RegisterPatch<SetupGroupableCharacterSelectorPatch>();
        patcher.RegisterPatch<SetupGroupableCharacterSelectButtonPatch>();
        patcher.RegisterPatch<SetupGroupableCharacterSelectButtonForModeScreensPatch>();
        patcher.RegisterPatch<InitSelectorPatch>();
        patcher.RegisterPatch<SelectGroupButtonIconPatch>();
        patcher.RegisterPatch<InterceptGroupButtonPatch>();
        patcher.RegisterPatch<AscensionPanelHostModePatch>();
        patcher.RegisterPatch<AscensionPanelMultiplayerModePatch>();
        patcher.RegisterPatch<AscensionPanelSingleplayerModePatch>();
    }
}

/// <summary>标准、Blitz、Dread 共用分组逻辑。三屏私有字段不同，
/// 按运行时类型反射并在字段缺失时降级。</summary>
internal static class CharacterSelectScreenSupport
{
    private static readonly Dictionary<Type, Dictionary<string, FieldInfo?>> FieldCache = new();

    private static FieldInfo? FindField(object screen, string name)
    {
        var type = screen.GetType();
        if (!FieldCache.TryGetValue(type, out var fields))
        {
            fields = new Dictionary<string, FieldInfo?>();
            FieldCache[type] = fields;
        }

        if (fields.TryGetValue(name, out var cached))
        {
            return cached;
        }

        FieldInfo? field = null;
        for (var current = type; current != null; current = current.BaseType)
        {
            field = current.GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (field != null)
            {
                break;
            }
        }

        fields[name] = field;
        return field;
    }

    internal static Control? GetBgContainer(object screen) =>
        FindField(screen, "_bgContainer")?.GetValue(screen) as Control;

    internal static Control? GetCharButtonContainer(object screen) =>
        FindField(screen, "_charButtonContainer")?.GetValue(screen) as Control;

    /// <summary>登记到 Dread 私有按钮列表，确保取消选中遍历覆盖分组按钮；
    /// 其他屏无该字段时跳过。</summary>
    internal static void AppendToButtonList(object screen, NCharacterSelectButton button)
    {
        if (FindField(screen, "_charSelectButtons")?.GetValue(screen) is List<NCharacterSelectButton> list)
        {
            list.Add(button);
        }
    }

    internal static Control? GetInfoPanel(object screen) =>
        FindField(screen, "_infoPanel")?.GetValue(screen) as Control;

    internal static NAscensionPanel? GetAscensionPanel(object screen) =>
        FindField(screen, "_ascensionPanel")?.GetValue(screen) as NAscensionPanel;

    /// <summary>创建分组按钮、隐藏原体按钮并重排焦点；buttonScene 为空时使用本体常量路径。</summary>
    internal static void SetupGroupButtons(Control container, ICharacterSelectButtonDelegate screenDelegate,
        PackedScene? buttonScene)
    {
        // 在插入前隐藏原体按钮与未识别为分组的占位按钮，避免误藏新分组按钮。
        // GroupState 只豁免有真实分组的按钮，不能给本体占位按钮创建空条目。
        var existing = container.GetChildren().OfType<NCharacterSelectButton>().ToList();
        foreach (var button in existing)
        {
            if (GroupableCharacterSelectorPatches.GroupState.TryGetValue(button, out var groupState) &&
                groupState.HasValue)
            {
                continue;
            }

            if (button.Character is IGroupableCharacter or GroupCharacterPlaceholder)
            {
                button.Visible = false;
            }
        }

        var insertIndex = GetInsertIndex(container, screenDelegate, existing);
        var alreadyGroupName = new HashSet<string>();

        foreach (var groupableCharacter in ModelDb.AllCharacters.OfType<IGroupableCharacter>())
        {
            var buttonName = groupableCharacter.Group.GetGroupInfo().Name;
            if (!alreadyGroupName.Add(buttonName))
            {
                continue;
            }

            var child = buttonScene != null
                ? buttonScene.Instantiate<NCharacterSelectButton>(PackedScene.GenEditState.Disabled)
                : SceneHelper.Instantiate<NCharacterSelectButton>("screens/char_select/char_select_button");

            child.Name = buttonName + "_button";
            container.AddChildSafely(child);
            container.MoveChild(child, ++insertIndex);
            child.Init(ModelDb.Character<GroupCharacterPlaceholder>(), screenDelegate);
            GroupableCharacterSelectorPatches.GroupState.Set(child, groupableCharacter.Group);
            CharacterSelectScreenSupport.AppendToButtonList(screenDelegate, child);

            BangDreamInSpireCore.Logger.Info($"Add Group Button: {buttonName}");

            var groupSelectIcon = groupableCharacter.Group.GetGroupSelectIcon();
            if (!string.IsNullOrEmpty(groupSelectIcon) && ResourceLoader.Exists(groupSelectIcon))
            {
                if (SelectGroupButtonIconPatch.IconField.GetValue(child) is TextureRect icon)
                {
                    icon.Texture = BangDreamPreloadManager.GetTexture2D(groupSelectIcon);
                }
            }
        }

        RewireFocus(container);

        // 记录插入数与最终可见数，便于发现按钮异常。
        var visibleButtons = container.GetChildren().OfType<NCharacterSelectButton>().Count(b => b.Visible);
        BangDreamInSpireCore.Logger.Info(
            $"Character select buttons on {screenDelegate.GetType().Name}: added {alreadyGroupName.Count} group button(s), {visibleButtons} visible in total.");
    }

    /// <summary>反射获取随机或轮换按钮，缺失返回 null。</summary>
    internal static NCharacterSelectButton? GetRandomButton(object screen) =>
        (FindField(screen, "_randomCharacterButton") ?? FindField(screen, "_rotatingCharacterButton"))?.GetValue(screen)
        as NCharacterSelectButton;

    /// <summary>优先定位随机或轮换按钮作为插入锚点；Dread 尚未 Init 时直接读字段，
    /// 失败则退回最后角色按钮之后。</summary>
    private static int GetInsertIndex(Control container, object screen, List<NCharacterSelectButton> buttons)
    {
        if (GetRandomButton(screen) is { } randomButton && randomButton.GetParent() == container)
        {
            return container.GetChildren().IndexOf(randomButton) - 1;
        }

        var lastCharacterIndex = -1;
        for (var i = buttons.Count - 1; i >= 0; --i)
        {
            var button = buttons[i];

            // 用状态标记排除随机或轮换按钮。
            if (button.IsRandom)
            {
                continue;
            }

            // 不写死本体角色表，避免漏掉新角色。
            if (!button.IsLocked && button.Character is not IGroupableCharacter and not GroupCharacterPlaceholder)
            {
                lastCharacterIndex = i;
                break;
            }
        }

        return lastCharacterIndex;
    }

    /// <summary>按可见按钮重排四向焦点。</summary>
    internal static void RewireFocus(Control container)
    {
        var visible = container.GetChildren().OfType<NCharacterSelectButton>()
            .Where(c => c.Visible)
            .ToList();
        if (visible.Count == 0)
        {
            return;
        }

        for (var index = 0; index < visible.Count; ++index)
        {
            var button = visible[index];
            button.FocusNeighborTop = button.GetPath();
            button.FocusNeighborBottom = button.GetPath();
            button.FocusNeighborLeft = index <= 0 ? visible[^1].GetPath() : visible[index - 1].GetPath();
            button.FocusNeighborRight = index < visible.Count - 1 ? visible[index + 1].GetPath() : visible[0].GetPath();
        }
    }
}

internal class HiddenUiPatch : IPatchMethod
{
    public static string PatchId => "on_selected_hidden_vanilla_ui";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter)),
            new ModPatchTarget(typeof(NBlitzScreen), nameof(NBlitzScreen.SelectCharacter)),
            new ModPatchTarget(typeof(NDreadScreen), nameof(NDreadScreen.SelectCharacter))
        ];
    }

    public static void Postfix(object __instance, NCharacterSelectButton charSelectButton)
    {
        CharacterSelectAdapter.Apply(__instance, charSelectButton.Character is GroupCharacterPlaceholder);
    }
}

internal class SetupGroupableCharacterSelectorPatch : IPatchMethod
{
    public static string PatchId => "on_selected_setup_custom_ui";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen._Ready)),
            new ModPatchTarget(typeof(NBlitzScreen), nameof(NBlitzScreen._Ready)),
            new ModPatchTarget(typeof(NDreadScreen), nameof(NDreadScreen._Ready))
        ];
    }

    public static void Postfix(object __instance)
    {
        var screen = (Node)__instance;
        if (CharacterSelectAdapter.For(__instance) is not { } adapter)
        {
            return;
        }

        adapter.Prepare(screen);

        // 覆盖层使用背景容器的位置与尺寸。
        var customControl = new Control { Name = "BangDreamSelectorHost" };
        if (adapter.GetBackground(__instance) is { } background)
        {
            customControl.Position = new Vector2(background.Position.X, background.Position.Y);
            customControl.Size = new Vector2(background.Size.X, background.Size.Y);
        }

        adapter.InsertOverlay(screen, customControl);

        var characterSelector = PreloadKey.CharacterSelector.GetScene().Instantiate<BangDreamCharacterSelector>();
        characterSelector.Name = "BangDreamSelector";
        characterSelector.Adapter = adapter;
        characterSelector.Visible = false;
        customControl.AddChildSafely(characterSelector);

        GroupableCharacterSelectorPatches.SetSelector(__instance, characterSelector);

        // 记录覆盖层父级与进阶支持。
        BangDreamInSpireCore.Logger.Info(
            $"[GroupSelect] {adapter.ModeName}屏覆盖层: index={customControl.GetIndex()}/{screen.GetChildCount()}, " +
            $"rect=({customControl.Position.X:F0},{customControl.Position.Y:F0}," +
            $"{customControl.Size.X:F0}x{customControl.Size.Y:F0}), ascension={adapter.SupportsAscension}");
    }
}

internal class SetupGroupableCharacterSelectButtonPatch : IPatchMethod
{
    public static string PatchId => "on_init_select_screen_setup_group_button";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCharacterSelectScreen), "InitCharacterButtons")];
    }

    [HarmonyPriority(1024)]
    public static void Postfix(NCharacterSelectScreen __instance,
        PackedScene ____charSelectButtonScene, Control ____charButtonContainer)
    {
        CharacterSelectScreenSupport.SetupGroupButtons(____charButtonContainer, __instance, ____charSelectButtonScene);
    }
}

/// <summary>Blitz / Dread 复用分组逻辑，按本体常量路径创建按钮。</summary>
internal class SetupGroupableCharacterSelectButtonForModeScreensPatch : IPatchMethod
{
    public static string PatchId => "on_init_mode_select_screen_setup_group_button";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(NBlitzScreen), "InitCharacterButtons"),
            new ModPatchTarget(typeof(NDreadScreen), "InitCharacterButtons")
        ];
    }

    [HarmonyPriority(1024)]
    public static void Postfix(object __instance, Control ____charButtonContainer)
    {
        CharacterSelectScreenSupport.SetupGroupButtons(____charButtonContainer,
            (ICharacterSelectButtonDelegate)__instance, buttonScene: null);
    }
}

internal class InitSelectorPatch : IPatchMethod
{
    public static string PatchId => "on_selected_init_content";

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.Select))];
    }

    private static bool IsSupportedScreen(ICharacterSelectButtonDelegate? candidate) =>
        candidate is NCharacterSelectScreen or NBlitzScreen or NDreadScreen;

    private static int _suppressInitDepth;

    /// <summary>在 Select 期间抑制选择器初始化。</summary>
    internal static void BeginSuppressInit() => _suppressInitDepth++;

    internal static void EndSuppressInit() => _suppressInitDepth = Math.Max(0, _suppressInitDepth - 1);

    public static void Prefix(NCharacterSelectButton __instance, bool ____isSelected,
        ICharacterSelectButtonDelegate ____delegate, CharacterModel ____character)
    {
        if (_suppressInitDepth > 0)
        {
            return;
        }

        if (!____isSelected && IsSupportedScreen(____delegate) && ____character is GroupCharacterPlaceholder)
        {
            // 只读取，避免把本体占位按钮标记为分组按钮。
            if (GroupableCharacterSelectorPatches.GroupState.TryGetValue(__instance, out var characterGroup) &&
                characterGroup.HasValue)
            {
                GroupableCharacterSelectorPatches.GetSelector(____delegate)
                    ?.Init(characterGroup.Value, __instance, ____delegate);
            }
            else
            {
                // 无分组条目时跳过初始化，由后续逻辑隐藏。
                BangDreamInSpireCore.Logger.Warn(
                    $"Placeholder character button {__instance.Name} has no character group; skipping init.");
            }
        }
    }
}

internal class SelectGroupButtonIconPatch : IPatchMethod
{
    public static string PatchId => "select_group_button_icon";

    internal static readonly FieldInfo IconField = AccessTools.Field(typeof(NCharacterSelectButton), "_icon");

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
            if (GroupableCharacterSelectorPatches.GroupState.TryGetValue(__instance, out var characterGroup) &&
                characterGroup.HasValue)
            {
                var groupSelectIcon = characterGroup.Value.GetGroupSelectIcon();
                if (!string.IsNullOrEmpty(groupSelectIcon) && ResourceLoader.Exists(groupSelectIcon))
                {
                    var texture = BangDreamPreloadManager.GetTexture2D(groupSelectIcon);
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
        return
        [
            new ModPatchTarget(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter)),
            new ModPatchTarget(typeof(NBlitzScreen), nameof(NBlitzScreen.SelectCharacter)),
            new ModPatchTarget(typeof(NDreadScreen), nameof(NDreadScreen.SelectCharacter))
        ];
    }

    public static bool Prefix(NCharacterSelectButton charSelectButton)
    {
        return charSelectButton.Character is not GroupCharacterPlaceholder;
    }
}

internal class AscensionPanelHostModePatch : IPatchMethod
{
    public static string PatchId => "bang_dream_style_ascension_panel_setup_host_mode";

    // Dread 无进阶面板。
    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(NCharacterSelectScreen),
                nameof(NCharacterSelectScreen.InitializeMultiplayerAsHost)),
            new ModPatchTarget(typeof(NBlitzScreen), nameof(NBlitzScreen.InitializeMultiplayerAsHost))
        ];
    }

    public static void Postfix(object __instance, StartRunLobby ____lobby)
    {
        if (CharacterSelectAdapter.For(__instance) is not { SupportsAscension: true } ||
            GroupableCharacterSelectorPatches.GetSelector(__instance) is not { } selector)
        {
            return;
        }

        selector.Lobby = ____lobby;
        selector.AscensionPanel.Initialize(MultiplayerUiMode.Host, ____lobby.GameMode);
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
                nameof(NCharacterSelectScreen.InitializeMultiplayerAsClient)),
            new ModPatchTarget(typeof(NBlitzScreen), nameof(NBlitzScreen.InitializeMultiplayerAsClient))
        ];
    }

    public static void Postfix(object __instance, StartRunLobby ____lobby)
    {
        if (CharacterSelectAdapter.For(__instance) is not { SupportsAscension: true } ||
            GroupableCharacterSelectorPatches.GetSelector(__instance) is not { } selector)
        {
            return;
        }

        selector.Lobby = ____lobby;
        selector.AscensionPanel.Initialize(MultiplayerUiMode.Client, ____lobby.GameMode);
    }
}

internal class AscensionPanelSingleplayerModePatch : IPatchMethod
{
    public static string PatchId => "bang_dream_style_ascension_panel_setup_singleplayer_mode";

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.InitializeSingleplayer)),
            new ModPatchTarget(typeof(NBlitzScreen), nameof(NBlitzScreen.InitializeSingleplayer))
        ];
    }

    public static void Postfix(object __instance, StartRunLobby ____lobby)
    {
        if (CharacterSelectAdapter.For(__instance) is not { SupportsAscension: true } ||
            GroupableCharacterSelectorPatches.GetSelector(__instance) is not { } selector)
        {
            return;
        }

        selector.Lobby = ____lobby;
        selector.AscensionPanel.Initialize(MultiplayerUiMode.Singleplayer, ____lobby.GameMode);
    }
}
