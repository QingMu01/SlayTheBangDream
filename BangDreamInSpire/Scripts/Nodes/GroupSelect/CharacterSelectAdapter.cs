using System.Runtime.CompilerServices;
using BangDreamInSpire.Scripts.Patches;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Blitz;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.Dread;

namespace BangDreamInSpire.Scripts.Nodes.GroupSelect;

/// <summary>适配标准、Blitz、Dread 的场景树与进阶差异，统一覆盖层插入与装饰显隐。</summary>
internal abstract class CharacterSelectAdapter
{
    private sealed class VanillaVisibility
    {
        public bool? Background;
        public bool? InfoPanel;
        public bool? AscensionPanel;
    }

    /// <summary>记录本体面板可见性，退出时按原值恢复。</summary>
    private static readonly ConditionalWeakTable<object, VanillaVisibility> Remembered = new();

    internal abstract string ModeName { get; }

    /// <summary>本屏是否支持进阶。</summary>
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

    /// <summary>创建覆盖层前处理屏幕特有问题。</summary>
    internal virtual void Prepare(Node screen)
    {
    }

    /// <summary>返回覆盖层的兄弟插入索引。</summary>
    internal abstract void InsertOverlay(Node screen, Control overlay);

    /// <summary>视图切换后处理装饰显隐。</summary>
    internal virtual void OnViewChanged(Node screen, bool groupView)
    {
    }

    /// <summary>切换分组与角色详情视图。</summary>
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

    /// <summary>默认插到背景容器之后。</summary>
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
