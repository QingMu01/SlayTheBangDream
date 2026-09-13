using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace BangDreamLib.Scripts.Utils;

/// <summary>
/// 特效脚本在「游戏内运行」与「Godot 编辑器 / F6 单独预览」两种环境下共用。
/// 本类集中收拢两者的差异：预览态判定、战斗状态判定、局外不可用的游戏数据兜底。
/// 特效脚本只向本类提问，不直接触碰 <see cref="ModelDb" /> / <see cref="CombatManager" />。
/// </summary>
public static class VfxPreviewSupport
{
    /// <summary>
    /// 局外预览用的桩调色板，取原版五个角色的主题色（StsColors 的红/绿/橙/紫/蓝）。
    /// ModelDb 未初始化时 <c>AllCharacters</c> 会抛 KeyNotFoundException，故不依赖它。
    /// </summary>
    private static readonly Color[] FallbackCharacterColors =
    [
        new("FF5555"),
        new("7FFF00"),
        new("FFA518"),
        new("EE82EE"),
        new("87CEEB"),
    ];

    /// <summary>
    /// 本节点是否正处于「单独预览」：编辑器里被编辑的场景根，或 F6 运行时的场景根。
    /// 游戏内特效都是被 AddChild 到容器/角色上的子节点，两种判定都不成立。
    /// 嵌套实例（如 music_wave 内嵌的 staff_ring_vfx）因此保持静默，不会误播。
    /// </summary>
    public static bool IsPreview(Node node)
    {
        var tree = node.GetTree();
        if (tree == null)
            return false;

        return Engine.IsEditorHint()
            ? tree.GetEditedSceneRoot() == node
            : tree.CurrentScene == node;
    }

    /// <summary>
    /// 真实战斗是否进行中。编辑器内恒为 true，避免预览时误判为「战斗已结束」。
    /// </summary>
    public static bool IsCombatLive => Engine.IsEditorHint() || CombatManager.Instance.IsInProgress;

    /// <summary>该特效当前是否允许继续播放：预览态或真实战斗进行中。</summary>
    public static bool ShouldContinuePlayback(Node node) => IsPreview(node) || IsCombatLive;

    /// <summary>
    /// 是否应在 <c>_Ready</c> 时自动播放。仅「运行中」成立：游戏内运行、或 F6 单独运行场景。
    /// 编辑器里编辑场景时一律为 false，场景保持静止、便于摆位设计；想查看效果请按 F6，
    /// 或手动勾选 Inspector 上的 <c>Play</c>。
    /// </summary>
    public static bool AutoPlayOnReady => !Engine.IsEditorHint();

    /// <summary>
    /// 是否正处于「F6 单独运行场景」的预览中（编辑器里编辑场景时为 false）。
    /// 用于只在真正运行预览时才响应参数改动而重播。
    /// </summary>
    public static bool IsPreviewRun(Node node) => !Engine.IsEditorHint() && IsPreview(node);

    /// <summary>
    /// 预览视口尺寸。F6 运行时即窗口逻辑尺寸（本项目为 1920×1080）。
    /// </summary>
    public static Vector2 PreviewViewportSize(Node node) =>
        node.GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920f, 1080f);

    /// <summary>把归一化坐标（各轴 0..1）换算为预览视口内的绝对坐标。</summary>
    public static Vector2 PreviewPoint(Node node, Vector2 fraction) =>
        PreviewViewportSize(node) * fraction;

    /// <summary>
    /// 把静止类特效摆到视口中心，避免出现在角落。仅在 F6 单独运行预览时生效：
    /// 游戏内位置由调用方设置，编辑器里保留作者的摆放（写改位置会把场景标记为已修改）。
    /// </summary>
    public static void CenterForPreview(Node2D node)
    {
        if (!IsPreviewRun(node))
            return;

        node.GlobalPosition = PreviewViewportSize(node) * 0.5f;
    }

    /// <summary>预览中移动类特效的默认起止点：y 取视口中线，x 取 1/4 到 3/4。</summary>
    public static (Vector2 Start, Vector2 End) PreviewTravelSpan(Node node)
    {
        var size = PreviewViewportSize(node);
        return (new Vector2(size.X * 0.25f, size.Y * 0.5f),
            new Vector2(size.X * 0.75f, size.Y * 0.5f));
    }

    /// <summary>
    /// 播放结束时是否应自我销毁。编辑器里永不销毁（否则节点会从被编辑的场景中消失），
    /// 预览态交给各特效循环重播，其余情况保持游戏原本的自毁行为。
    /// </summary>
    public static bool ShouldSelfFree(Node node) =>
        !Engine.IsEditorHint() && !IsPreview(node);

    /// <summary>
    /// 取一个角色主题色用于特效着色：预览态使用桩调色板，游戏内使用真实角色色。
    /// 不做缓存——若首次调用发生在 ModelDb 就绪前，缓存会把桩色残留整个进程。
    /// </summary>
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

            GD.PushWarning("[BangDreamLib] 角色列表为空，特效着色回退到桩调色板。");
        }
        catch (Exception exception)
        {
            // 仅在真实游戏流程中告警；编辑器 / 预览走的是桩调色板，属预期行为。
            GD.PushWarning(
                $"[BangDreamLib] 角色主题色不可用，回退到桩调色板：{exception.GetType().Name}");
        }

        return FallbackCharacterColors;
    }
}
