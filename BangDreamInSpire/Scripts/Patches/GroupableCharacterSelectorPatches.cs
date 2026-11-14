using System.Reflection;
using System.Runtime.CompilerServices;
using BangDreamInSpire.Scripts.Character;
using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Nodes;
using BangDreamInSpire.Scripts.Utils;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
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

    /// <summary>
    /// 每个角色选择屏各自的定制选择器。
    /// <para>
    /// **不能用单一静态字段**：<c>NMainMenuSubmenuStack._Ready</c> 会在启动时同时实例化
    /// <see cref="NCharacterSelectScreen" /> 与 <see cref="NDreadScreen" />，两个屏的 <c>_Ready</c> 后置都会跑，
    /// 单一静态会被后跑的那个覆盖，导致在某屏里操作到另一屏的节点。
    /// </para>
    /// </summary>
    private static readonly ConditionalWeakTable<object, BangDreamCharacterSelector> Selectors = new();

    internal static BangDreamCharacterSelector? GetSelector(object? screen) =>
        screen != null && Selectors.TryGetValue(screen, out var selector) ? selector : null;

    internal static void SetSelector(object screen, BangDreamCharacterSelector selector) =>
        Selectors.AddOrUpdate(screen, selector);

    /// <summary>
    /// 把 <paramref name="character" /> 真正提交为当前选择。
    /// <para>
    /// 用本体为**真实角色**生成的按钮（分组逻辑已把它隐藏）走一遍完整的原生
    /// <c>SelectCharacter</c>：播放选择音效、启用出航按钮、清空并重建背景、写入
    /// <c>_lobby.SetLocalCharacter</c>，最后把其它按钮全部 <c>Deselect</c>。
    /// </para>
    /// <para>
    /// 为什么必须用那个隐藏按钮而不是分组按钮：三个屏幕后续都会读
    /// <c>_selectedButton.Character</c>（保存「上次选择的角色」以及 Dread 的
    /// <c>SetLocalCharacter(_selectedButton.Character, …)</c>），若让它指向承载占位角色的分组按钮，
    /// 这些地方会拿到 <see cref="GroupCharacterPlaceholder" />。
    /// </para>
    /// </summary>
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
            // 原生流程把选中态放在上面那个不可见按钮上，并顺手把分组按钮 Deselect 掉；
            // 但玩家看到的是分组按钮，这里补回选中态。抑制 Init，避免再次打开/重置选择器。
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

    /// <summary>切回「分组选择器」视图：隐藏本体信息面板/背景，显示自定义选择器。</summary>
    internal static void ApplyGroupView(object screen)
    {
        var infoPanel = CharacterSelectScreenSupport.GetInfoPanel(screen);
        if (infoPanel != null)
        {
            infoPanel.Visible = false;
        }

        var ascensionPanel = CharacterSelectScreenSupport.GetAscensionPanel(screen);
        if (ascensionPanel != null)
        {
            ascensionPanel.Visible = false;
        }

        var bgContainer = CharacterSelectScreenSupport.GetBgContainer(screen);
        if (bgContainer != null)
        {
            bgContainer.Visible = false;
        }

        var selector = GetSelector(screen);
        if (selector != null)
        {
            selector.Visible = true;
        }
    }

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

/// <summary>
/// 角色选择屏的共用逻辑。
/// <para>
/// 本体 0.113 把角色选择屏复制成了多份：标准 <see cref="NCharacterSelectScreen" />、
/// <see cref="NBlitzScreen" />、<see cref="NDreadScreen" />（另有 <c>NCustomRunScreen</c>，本模组不支持）。
/// 三者都继承 <c>NSubmenu</c> 并实现 <c>ICharacterSelectButtonDelegate</c>，
/// <c>SelectCharacter</c> / <c>InitCharacterButtons</c> / <c>_Ready</c> 同名同签名，
/// 但**私有字段集合不同**：
/// </para>
/// <list type="bullet">
/// <item>Dread 没有 <c>_infoPanel</c> 与 <c>_ascensionPanel</c>；</item>
/// <item>Blitz / Dread 没有 <c>_charSelectButtonScene</c>（改用 <c>SceneHelper</c> + 常量路径建按钮）。</item>
/// </list>
/// <para>
/// 因此这里按**实例运行时类型**反射取字段，缺失即返回 null，让同一份补丁体服务三个屏幕，
/// 避免为每个屏复制一遍补丁逻辑（否则三份私有方法名各改一次就要维护三处）。
/// </para>
/// </summary>
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

    /// <summary>
    /// 把分组按钮登记进屏幕自己的按钮列表（<c>_charSelectButtons</c>）。
    /// <para>
    /// <c>NDreadScreen.SelectCharacter</c> 取消其它按钮选中态时遍历的是这个私有列表、而不是容器；
    /// 分组按钮直接挂在容器里，不登记的话「选中别的角色后分组按钮仍保持高亮」。
    /// 其它两个屏幕遍历容器，没有该字段，这里静默跳过。
    /// </para>
    /// </summary>
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

    /// <summary>
    /// 为每个角色分组建一个占位按钮并挂到按钮容器末尾，随后隐藏本体为 <see cref="IGroupableCharacter" /> 生成的按钮，
    /// 再按可见按钮重排焦点环。
    /// </summary>
    /// <param name="buttonScene">
    /// 本体的按钮场景。标准屏有 <c>_charSelectButtonScene</c> 字段，直接透传即可；
    /// Blitz / Dread 没有该字段，传 null 时按本体的常量路径实例化。
    /// </param>
    internal static void SetupGroupButtons(Control container, ICharacterSelectButtonDelegate screenDelegate,
        PackedScene? buttonScene)
    {
        // 先隐藏本体生成的按钮：
        // - IGroupableCharacter：模组角色，已由分组按钮代表；
        // - GroupCharacterPlaceholder：分组占位角色。标准屏由 RitsuLib 的可见性策略在
        //   InitCharacterButtons 阶段过滤掉（它只识别 NCharacterSelectScreen），Blitz / Dread
        //   不在其识别范围内，占位按钮会真的出现在界面上，这里统一兜底隐藏。
        //
        // **必须在插入分组按钮之前做**：分组按钮自身也是用 GroupCharacterPlaceholder 作为 Character，
        // 若放到插入之后按 Character 类型扫描，会把刚插入的分组按钮一起隐藏掉（角色选择按钮整体消失）。
        var existing = container.GetChildren().OfType<NCharacterSelectButton>().ToList();
        foreach (var button in existing)
        {
            // 分组按钮自身也用 GroupCharacterPlaceholder 作为 Character，必须靠 GroupState 区分；
            // 这样即便将来有人把这段挪到插入之后，也不会再把分组按钮一起隐藏。
            if (GroupableCharacterSelectorPatches.GroupState.ContainsKey(button))
            {
                continue;
            }

            if (button.Character is IGroupableCharacter or GroupCharacterPlaceholder)
            {
                button.Visible = false;
            }
        }

        var insertIndex = GetInsertIndex(existing);
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

        // 诊断：分组按钮被误隐藏过一次（隐藏扫描放在插入之后，把同样是 Placeholder 的分组按钮一起藏了），
        // 这里把「插入数 / 最终可见数」打进日志，便于下次直接看出按钮数量异常。
        var visibleButtons = container.GetChildren().OfType<NCharacterSelectButton>().Count(b => b.Visible);
        BangDreamInSpireCore.Logger.Info(
            $"Character select buttons on {screenDelegate.GetType().Name}: added {alreadyGroupName.Count} group button(s), {visibleButtons} visible in total.");
    }

    private static int GetInsertIndex(List<NCharacterSelectButton> buttons)
    {
        var lastCharacterIndex = -1;
        for (var i = buttons.Count - 1; i >= 0; --i)
        {
            var button = buttons[i];
            if (!button.IsLocked && SetupGroupableCharacterSelectButtonPatch.VanillaCharacters.Contains(button.Character))
            {
                lastCharacterIndex = i;
                break;
            }
        }

        return lastCharacterIndex;
    }

    /// <summary>按可见按钮重排四向焦点环（本体原逻辑的等价重放）。</summary>
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
        var infoPanel = CharacterSelectScreenSupport.GetInfoPanel(__instance);
        var ascensionPanel = CharacterSelectScreenSupport.GetAscensionPanel(__instance);
        var bgContainer = CharacterSelectScreenSupport.GetBgContainer(__instance);

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
            var selector = GroupableCharacterSelectorPatches.GetSelector(__instance);
            if (selector != null)
            {
                selector.Visible = !bgContainer.Visible;
            }
        }
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
        var bgContainer = CharacterSelectScreenSupport.GetBgContainer(__instance);

        var customControl = new Control();
        if (bgContainer != null)
        {
            customControl.Position = new Vector2(bgContainer.Position.X, bgContainer.Position.Y);
            customControl.Size = new Vector2(bgContainer.Size.X, bgContainer.Size.Y);
        }

        screen.AddChildSafely(customControl);
        if (bgContainer != null)
        {
            var originalIndex = screen.GetChildren().IndexOf(bgContainer);
            if (originalIndex >= 0)
            {
                screen.MoveChild(customControl, originalIndex + 1);
            }
        }

        var characterSelector = PreloadKey.CharacterSelector.GetScene().Instantiate<BangDreamCharacterSelector>();
        characterSelector.Name = "BangDreamSelector";
        characterSelector.Visible = false;
        customControl.AddChildSafely(characterSelector);

        GroupableCharacterSelectorPatches.SetSelector(__instance, characterSelector);
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
    internal static readonly List<CharacterModel> VanillaCharacters =
    [
        ModelDb.Character<Ironclad>(),
        ModelDb.Character<Silent>(),
        ModelDb.Character<Regent>(),
        ModelDb.Character<Necrobinder>(),
        ModelDb.Character<Defect>()
    ];

    [HarmonyPriority(1024)]
    public static void Postfix(NCharacterSelectScreen __instance,
        PackedScene ____charSelectButtonScene, Control ____charButtonContainer)
    {
        CharacterSelectScreenSupport.SetupGroupButtons(____charButtonContainer, __instance, ____charSelectButtonScene);
    }
}

/// <summary>
/// Blitz / Dread 的角色选择屏各有一份自己的 <c>InitCharacterButtons</c>（私有，且不通过
/// <c>_charSelectButtonScene</c> 字段建按钮）。此处复用同一套分组逻辑，按钮场景按本体的常量路径实例化。
/// </summary>
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

    /// <summary>在 <c>Select()</c> 期间抑制选择器初始化（用于以编程方式恢复按钮选中态）。</summary>
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
            var characterGroup = GroupableCharacterSelectorPatches.GroupState.GetOrCreate(__instance);
            if (characterGroup.HasValue)
            {
                GroupableCharacterSelectorPatches.GetSelector(____delegate)?.Init(characterGroup.Value, __instance, ____delegate);
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
            var characterGroup = GroupableCharacterSelectorPatches.GroupState.GetOrCreate(__instance);
            if (characterGroup.HasValue)
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

    // Dread 屏没有飞升面板，故只挂标准屏与 Blitz 屏。
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
        var selector = GroupableCharacterSelectorPatches.GetSelector(__instance);
        if (selector != null)
        {
            selector.Lobby = ____lobby;
            selector.AscensionPanel.Initialize(MultiplayerUiMode.Host, ____lobby.GameMode);
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
                nameof(NCharacterSelectScreen.InitializeMultiplayerAsClient)),
            new ModPatchTarget(typeof(NBlitzScreen), nameof(NBlitzScreen.InitializeMultiplayerAsClient))
        ];
    }

    public static void Postfix(object __instance, StartRunLobby ____lobby)
    {
        var selector = GroupableCharacterSelectorPatches.GetSelector(__instance);
        if (selector != null)
        {
            selector.Lobby = ____lobby;
            selector.AscensionPanel.Initialize(MultiplayerUiMode.Client, ____lobby.GameMode);
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
            new ModPatchTarget(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.InitializeSingleplayer)),
            new ModPatchTarget(typeof(NBlitzScreen), nameof(NBlitzScreen.InitializeSingleplayer))
        ];
    }

    public static void Postfix(object __instance, StartRunLobby ____lobby)
    {
        var selector = GroupableCharacterSelectorPatches.GetSelector(__instance);
        if (selector != null)
        {
            selector.Lobby = ____lobby;
            selector.AscensionPanel.Initialize(MultiplayerUiMode.Singleplayer, ____lobby.GameMode);
        }
    }
}
