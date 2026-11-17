using System.Runtime.CompilerServices;
using BangDreamInSpire.Scripts.Patches;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Blitz;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.Dread;

namespace BangDreamInSpire.Scripts.Nodes.GroupSelect;

/// <summary>
/// 三个角色选择屏（标准 / 快速 / 恐惧）的差异适配器。
/// <para>
/// 本体把角色选择屏复制成了三份独立的类（<see cref="NCharacterSelectScreen" />、
/// <see cref="NBlitzScreen" />、<see cref="NDreadScreen" />），它们字段集合与场景树都不一样：
/// </para>
/// <list type="bullet">
/// <item>标准 / 快速：背景 <c>AnimatedBg</c> 是根节点的直接子节点，指令按钮（确认 / 返回）在它之后；</item>
/// <item>快速：场景额外有一个装饰性大字 <c>BlitzPlaceholder</c>，位于屏幕中上方、树序在所有按钮之后；</item>
/// <item>恐惧：背景在 <c>SubViewportContainer/SubViewport</c> 里（不是根节点直接子节点），
/// 且没有角色详情面板与进阶面板。</item>
/// </list>
/// <para>
/// 基线实现统一按「背景容器的下一个兄弟节点」插覆盖层，在恐惧屏上索引解析失败、
/// 覆盖层被追加到末尾，把确认 / 返回整个盖住；快速屏则被装饰大字挡住成员书签的点击。
/// 这里把每个屏的「覆盖层插哪、哪些装饰要临时让位、有没有进阶」收口成适配器，
/// 调用点只面对 <see cref="CharacterSelectAdapter" />。
/// </para>
/// </summary>
internal abstract class CharacterSelectAdapter
{
    private sealed class VanillaVisibility
    {
        public bool? Background;
        public bool? InfoPanel;
        public bool? AscensionPanel;
    }

    /// <summary>
    /// 进入分组视图前本体各面板的可见性。退出分组视图时按原值恢复，
    /// 而不是一律置 true（例如恐惧屏本来就没有进阶面板）。
    /// </summary>
    private static readonly ConditionalWeakTable<object, VanillaVisibility> Remembered = new();

    internal abstract string ModeName { get; }

    /// <summary>本屏是否有进阶（飞升）概念。恐惧模式固定 10 级、玩家不可选，因此没有。</summary>
    internal abstract bool SupportsAscension { get; }

    internal static CharacterSelectAdapter? For(object? screen) => screen switch
    {
        NCharacterSelectScreen => StandardSelectAdapter.Instance,
        NBlitzScreen => BlitzSelectAdapter.Instance,
        NDreadScreen => DreadSelectAdapter.Instance,
        _ => null
    };

    internal virtual Control? GetBackground(object screen) =>
        CharacterSelectScreenSupport.GetBgContainer(screen);

    internal virtual Control? GetInfoPanel(object screen) =>
        CharacterSelectScreenSupport.GetInfoPanel(screen);

    internal virtual NAscensionPanel? GetVanillaAscensionPanel(object screen) =>
        CharacterSelectScreenSupport.GetAscensionPanel(screen);

    /// <summary>屏幕 <c>_Ready</c> 之后、创建覆盖层之前调用；处理本屏特有的输入 / 层级问题。</summary>
    internal virtual void Prepare(Node screen)
    {
    }

    /// <summary>把覆盖层插到「背景之上、所有本体 chrome 之下」，返回实际插入的兄弟索引。</summary>
    internal abstract void InsertOverlay(Node screen, Control overlay);

    /// <summary>分组视图 / 角色详情视图切换后调用；处理本屏特有的装饰节点显隐。</summary>
    internal virtual void OnViewChanged(Node screen, bool groupView)
    {
    }

    /// <summary>
    /// 切换整屏呈现：分组视图隐藏本体背景 / 详情 / 进阶并显示自定义选择器，
    /// 角色详情视图按进入前记住的可见性恢复。
    /// </summary>
    internal static void Apply(object screen, bool groupView)
    {
        if (For(screen) is not { } adapter || screen is not Node screenNode)
        {
            return;
        }

        var background = adapter.GetBackground(screen);
        var infoPanel = adapter.GetInfoPanel(screen);
        var ascensionPanel = adapter.GetVanillaAscensionPanel(screen);
        var selector = GroupableCharacterSelectorPatches.GetSelector(screen);

        if (groupView)
        {
            var remembered = Remembered.GetOrCreateValue(screen);
            remembered.Background ??= IsVisible(background);
            remembered.InfoPanel ??= IsVisible(infoPanel);
            remembered.AscensionPanel ??= IsVisible(ascensionPanel);

            SetVisible(background, false);
            SetVisible(infoPanel, false);
            SetVisible(ascensionPanel, false);
        }
        else if (Remembered.TryGetValue(screen, out var remembered))
        {
            SetVisible(background, remembered.Background ?? true);
            SetVisible(infoPanel, remembered.InfoPanel ?? true);
            SetVisible(ascensionPanel, remembered.AscensionPanel ?? false);
        }

        if (selector != null)
        {
            selector.Visible = groupView;
        }

        adapter.OnViewChanged(screenNode, groupView);
    }

    /// <summary>覆盖层默认插入点：紧跟背景容器之后（标准 / 快速屏均满足「背景之上、chrome 之下」）。</summary>
    protected void InsertAfterBackground(Node screen, Control overlay)
    {
        screen.AddChildSafely(overlay);
        if (GetBackground(screen) is not { } background)
        {
            BangDreamInSpireCore.Logger.Warn(
                $"[GroupSelect] {screen.GetType().Name} 上找不到背景容器，覆盖层将留在末尾。");
            return;
        }

        var index = screen.GetChildren().IndexOf(background);
        if (index >= 0)
        {
            screen.MoveChild(overlay, index + 1);
        }
        else
        {
            BangDreamInSpireCore.Logger.Warn(
                $"[GroupSelect] {screen.GetType().Name} 的背景容器不是根节点直接子节点，覆盖层将留在末尾。");
        }
    }

    private static bool? IsVisible(CanvasItem? item) =>
        item != null && GodotObject.IsInstanceValid(item) ? item.Visible : null;

    private static void SetVisible(CanvasItem? item, bool visible)
    {
        if (item != null && GodotObject.IsInstanceValid(item))
        {
            item.Visible = visible;
        }
    }
}
