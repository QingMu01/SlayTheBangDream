using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace BangDreamInSpire.Scripts.Utils;

/// <summary>隔离游戏运行与 Godot 编辑器/F6 预览的生命周期及数据访问差异。</summary>
public static class VfxPreviewSupport
{
    /// <summary>预览使用固定主题色；ModelDb 未初始化时 AllCharacters 会抛 KeyNotFoundException。</summary>
    private static readonly Color[] FallbackCharacterColors =
    [
        new("FF5555"),
        new("7FFF00"),
        new("FFA518"),
        new("EE82EE"),
        new("87CEEB"),
    ];

    /// <summary>仅编辑器或 F6 的场景根属于预览；游戏内子节点及嵌套实例不会误触发预览播放。</summary>
    public static bool IsPreview(Node node)
    {
        var tree = node.GetTree();
        if (tree == null)
            return false;

        return Engine.IsEditorHint()
            ? tree.GetEditedSceneRoot() == node
            : tree.CurrentScene == node;
    }

    /// <summary>编辑器内恒为 true，避免预览被判定为战斗结束。</summary>
    public static bool IsCombatLive => Engine.IsEditorHint() || CombatManager.Instance.IsInProgress;

    public static bool ShouldContinuePlayback(Node node) => IsPreview(node) || IsCombatLive;

    /// <summary>编辑器内保持静止以便摆位；仅游戏运行或 F6 预览时自动播放。</summary>
    public static bool AutoPlayOnReady => !Engine.IsEditorHint();

    /// <summary>仅 F6 运行预览时响应参数改动并重播。</summary>
    public static bool IsPreviewRun(Node node) => !Engine.IsEditorHint() && IsPreview(node);

    public static Vector2 PreviewViewportSize(Node node) =>
        node.GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920f, 1080f);

    public static Vector2 PreviewPoint(Node node, Vector2 fraction) =>
        PreviewViewportSize(node) * fraction;

    /// <summary>仅 F6 预览时居中；游戏内由调用方定位，编辑器内避免改写位置而弄脏场景。</summary>
    public static void CenterForPreview(Node2D node)
    {
        if (!IsPreviewRun(node))
            return;

        node.GlobalPosition = PreviewViewportSize(node) * 0.5f;
    }

    public static (Vector2 Start, Vector2 End) PreviewTravelSpan(Node node)
    {
        var size = PreviewViewportSize(node);
        return (new Vector2(size.X * 0.25f, size.Y * 0.5f),
            new Vector2(size.X * 0.75f, size.Y * 0.5f));
    }

    /// <summary>编辑器和预览中不自毁，以免删除场景节点并支持循环重播。</summary>
    public static bool ShouldSelfFree(Node node) =>
        !Engine.IsEditorHint() && !IsPreview(node);

    /// <summary>预览使用桩色、游戏内使用角色色；不缓存以免 ModelDb 就绪前的桩色残留整个进程。</summary>
    public static Color RandomCharacterColor(Node context)
    {
        return Rng.Chaotic.NextItem(ResolveCharacterColors(context));
    }

    private static Color[] ResolveCharacterColors(Node context)
    {
        if (IsPreview(context) || Engine.IsEditorHint())
            return FallbackCharacterColors;

        try
        {
            var colors = ModelDb.AllCharacters.Select(character => character.NameColor).ToArray();
            if (colors.Length > 0)
                return colors;

            GD.PushWarning("[BangDreamInSpire] 角色列表为空，特效着色回退到桩调色板。");
        }
        catch (Exception exception)
        {
            GD.PushWarning(
                $"[BangDreamInSpire] 角色主题色不可用，回退到桩调色板：{exception.GetType().Name}");
        }

        return FallbackCharacterColors;
    }
}
