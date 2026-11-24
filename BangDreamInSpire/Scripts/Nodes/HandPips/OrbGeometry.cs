using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace BangDreamInSpire.Scripts.Nodes.HandPips;

internal static partial class OrbRenderer
{
    private static Block ComputeBlock(int total, int pixels, float scaleFactor)
    {
        if (_blockUsed >= BlockPool.Count)
        {
            BlockPool.Add(new Block());
        }

        Block block = BlockPool[_blockUsed++];
        block.Total = Math.Max(0, total);
        int cap = Config.Data.MaxPerRow;
        block.Cols = cap > 0 ? Math.Min(cap, Math.Max(block.Total, 1)) : Math.Max(block.Total, 1);
        float scale = Mathf.Clamp(1f - 0.06f * (block.Cols - 4), 0.62f, 1f) * scaleFactor;
        block.Spacing = (Mathf.Clamp(Config.Data.RowSpacing, 24, 80) / 50f) * BaseSpacing * scale * 0.81f;
        block.Size = (Mathf.Clamp(pixels, 16, 128) / 46f) * scale * 0.81f;
        block.Rows = block.Total <= 0 ? 0 : (block.Total + block.Cols - 1) / block.Cols;
        float orbPx = 46f * block.Size;
        block.Step = orbPx + Mathf.Max(4f, orbPx * 0.16f);
        block.Height = block.Rows > 1 ? (block.Rows - 1) * block.Step : 0f;
        block.Width = Math.Max(0, block.Cols - 1) * block.Spacing;
        return block;
    }

    /// <summary>设置页改了位置/尺寸：下一帧整排直接吸附过去，不要拖一条长尾。</summary>
    internal static void InvalidatePlacement()
    {
        _snap = true;
    }

    /// <summary>副资源行相对主能量行的水平偏移：左缘对齐 / 右缘对齐 / 中心对齐。</summary>
    private static float AlignOffset(float mainWidth, float rowWidth)
    {
        switch (Config.Data.Align)
        {
            case RowAlign.Left:
                return -(mainWidth * 0.5f) + rowWidth * 0.5f;
            case RowAlign.Right:
                return mainWidth * 0.5f - rowWidth * 0.5f;
            default:
                return 0f;
        }
    }

    private static AccessTools.FieldRef<NHandCardHolder, Vector2> _targetScaleRef;
    private static bool _targetScaleTried;

    private static bool TryTargetScale(NHandCardHolder holder, out Vector2 scale)
    {
        scale = Vector2.One;
        if (!_targetScaleTried)
        {
            _targetScaleTried = true;
            try
            {
                _targetScaleRef = AccessTools.FieldRefAccess<NHandCardHolder, Vector2>("_targetScale");
            }
            catch (Exception e)
            {
                GD.PushWarning($"[BangDreamInSpire/handpips] _targetScale missing: {e.Message}");
            }
        }

        if (_targetScaleRef == null)
        {
            return false;
        }

        try
        {
            scale = _targetScaleRef(holder);
            return true;
        }
        catch
        {
            _targetScaleRef = null;
            return false;
        }
    }

    private static readonly HashSet<ulong> LandedSeen = new();
    private static readonly List<(Row row, Block block, float y, int total)> SecBlocksBuf = new(8);
    private static readonly List<Block> BlockPool = new(8);
    private static int _blockUsed;
    private static readonly Dictionary<ulong, TextureRect> HeadCache = new();
    private static readonly HashSet<string> SeenBuf = new(8);
    private static readonly List<KeyValuePair<string, Row>> RemoveBuf = new(8);
    private static readonly StringName P_Grey = "grey";
    private static readonly StringName P_White = "white";
    private static readonly StringName P_Tint = "tint";
    private static readonly StringName P_Phase = "phase";
    private static readonly StringName P_Strength = "strength";

    // 每帧都会跑到的 meta 键 / 着色器参数名。字符串字面量每次调用都要做一次
    // string -> StringName 的转换（Godot 内部还要按名查表），这里统一提成静态常量。
    private static readonly StringName M_Life = "life";
    private static readonly StringName M_Vx = "vx";
    private static readonly StringName M_Vy = "vy";
    private static readonly StringName M_Spin = "spin";
    private static readonly StringName P_Progress = "progress";

    /// <summary>跨类（CardBob）用的 progress 参数名，避免那边再写一次字符串字面量。</summary>
    internal static StringName ProgressParam => P_Progress;

    /// <summary>照原版 CardLanding：缩放已经到位（或曾经到位过）的卡才算落位。</summary>
    private static bool Landed(NHandCardHolder holder, NHandCardHolder focused)
    {
        try
        {
            ulong key = holder.GetInstanceId();
            if (LandedSeen.Contains(key))
            {
                return true;
            }

            if (holder != focused)
            {
                if (TryTargetScale(holder, out Vector2 targetScale)
                    && Mathf.Abs(holder.Scale.X - targetScale.X) > 0.01f)
                {
                    return false;
                }
            }

            if (LandedSeen.Count > 256)
            {
                LandedSeen.Clear(); // 上限保护：也顺带清掉可能被复用的旧 instanceId
            }

            LandedSeen.Add(key);
            return true;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>按卡牌实例缓存 Head 节点，避免每帧对每张牌递归 GetChildren。</summary>
    internal static TextureRect FindHeadCached(NHandCardHolder holder)
    {
        if (holder == null || !GodotObject.IsInstanceValid(holder))
        {
            return null;
        }

        ulong key = holder.GetInstanceId();
        if (HeadCache.TryGetValue(key, out TextureRect cached)
            && cached != null && GodotObject.IsInstanceValid(cached))
        {
            return cached;
        }

        // 从卡牌子树里找 Head；绝不能调用 FindHeadCached 自己（同签名自递归 -> StackOverflow）
        TextureRect found = CardBob.FindHead(holder.CardNode, 0);
        if (found != null)
        {
            if (HeadCache.Count > 64)
            {
                HeadCache.Clear(); // 上限保护：卡牌实例会不断变化
            }

            HeadCache[key] = found;
        }

        return found;
    }

    private static float CardEdge(NHandCardHolder holder, bool right)
    {
        try
        {
            Rect2 rect = holder.Hitbox.GetGlobalRect();
            return right ? rect.End.X : rect.Position.X;
        }
        catch
        {
            Rect2 rect = holder.GetGlobalRect();
            return right ? rect.End.X : rect.Position.X;
        }
    }

    private static float LeftmostCardEdge(IReadOnlyList<NHandCardHolder> holders)
    {
        float edge = float.MaxValue;
        foreach (NHandCardHolder holder in holders)
        {
            if (holder == null || !GodotObject.IsInstanceValid(holder) || !holder.IsVisibleInTree())
            {
                continue;
            }

            edge = Mathf.Min(edge, CardEdge(holder, false));
        }

        return edge == float.MaxValue ? 0f : edge;
    }

    private static float RightmostCardEdge(IReadOnlyList<NHandCardHolder> holders)
    {
        float edge = float.MinValue;
        foreach (NHandCardHolder holder in holders)
        {
            if (holder == null || !GodotObject.IsInstanceValid(holder) || !holder.IsVisibleInTree())
            {
                continue;
            }

            edge = Mathf.Max(edge, CardEdge(holder, true));
        }

        return edge == float.MinValue ? 0f : edge;
    }

    /// <summary>自由摆放：按住球排拖动改坐标，松手写盘。只读鼠标状态，不往游戏塞输入事件。</summary>
    private static Vector2 DragIfGrabbed(Overlay overlay, Vector2 target, float halfWidth, float blockTop)
    {
        bool pressed = Input.IsMouseButtonPressed(MouseButton.Left);

        // 正在拖角缩放：这一下点击归变换框，不允许顺手把整排也拖走
        if (overlay.GripActive >= 0)
        {
            overlay.DragLatch = pressed;
            return target;
        }

        Vector2 mouse = overlay.Root.GetGlobalMousePosition();

        if (!overlay.Dragging)
        {
            if (pressed && !overlay.DragLatch)
            {
                float top = target.Y + blockTop - 22f;
                float bottom = target.Y + 26f;
                var box = new Rect2(target.X - halfWidth - 32f, top, halfWidth * 2f + 64f, bottom - top);
                if (box.HasPoint(mouse))
                {
                    overlay.Dragging = true;
                    overlay.GrabOffset = mouse - target;
                    GD.Print("[BangDreamInSpire/handpips] free place: grabbing row");
                }
            }
        }
        else
        {
            var view = overlay.Root.GetViewportRect().Size;
            Config.Data.FreeX = Mathf.Clamp(mouse.X - overlay.GrabOffset.X, 24f, Mathf.Max(24f, view.X - 24f));
            Config.Data.FreeY = Mathf.Clamp(mouse.Y - overlay.GrabOffset.Y, 72f, Mathf.Max(72f, view.Y - 72f));
            target = new Vector2(Config.Data.FreeX, Config.Data.FreeY);
            if (!pressed)
            {
                overlay.Dragging = false;
                Config.Save();
                GD.Print($"[BangDreamInSpire/handpips] free place saved ({Config.Data.FreeX:F0},{Config.Data.FreeY:F0})");
            }
        }

        overlay.DragLatch = pressed;
        return target;
    }

    /// <summary>
    /// 自由摆放的变换框：尺寸跟着当前球排走（Root 局部单位，世界尺寸 = 局部 × FreeScale）。
    /// 宽 = 球排宽 + 球半径 + 内边距；高 = 行栈高 + 一球高 + 内边距（单排时约 61 px，落在 60~80 区间）。
    /// </summary>
    /// <summary>
    /// 自由摆放的变换框：用普通 Line2D / Polygon2D 画（和球同一条渲染路径，不依赖自定义节点的 _Draw）。
    /// 四角可拖：按「鼠标到框心距离 / 按下时距离」等比改 FreeScale，并反向移动 FreeX/FreeY 让框心不动。
    /// </summary>
    private static void UpdateGrip(Overlay overlay, Block blockE, float delta)
    {
        if (overlay.GripRoot == null || !GodotObject.IsInstanceValid(overlay.GripRoot))
        {
            return;
        }

        float scale = Mathf.Clamp(Config.Data.FreeScale, 0.5f, 2.5f);
        float inv = 1f / Mathf.Max(0.05f, scale);
        float orbPx = 46f * blockE.Size;
        float rowWidth = Mathf.Max(overlay.Energy.LastWidth, blockE.Width);
        float halfW = rowWidth * 0.5f + orbPx * 0.5f + 14f;
        float halfH = (blockE.Height + orbPx) * 0.5f + 12f;
        float centreY = -blockE.Height * 0.5f;

        Vector2 tl = new(-halfW, centreY - halfH);
        Vector2 tr = new(halfW, centreY - halfH);
        Vector2 br = new(halfW, centreY + halfH);
        Vector2 bl = new(-halfW, centreY + halfH);
        float width = Mathf.Max(0.6f, 1.2f * inv);

        overlay.GripTop.Points = new[] { tl, tr };
        overlay.GripBottom.Points = new[] { bl, br };
        overlay.GripLeft.Points = new[] { tl, bl };
        overlay.GripRight.Points = new[] { tr, br };
        overlay.GripTop.Width = width;
        overlay.GripBottom.Width = width;
        overlay.GripLeft.Width = width;
        overlay.GripRight.Width = width;

        Vector2[] corners = { tl, tr, br, bl };
        for (int i = 0; i < 4; i++)
        {
            Polygon2D handle = overlay.GripHandles[i];
            if (handle == null || !GodotObject.IsInstanceValid(handle))
            {
                continue;
            }

            handle.Position = corners[i];
            handle.Scale = Vector2.One * (9f * inv / 8f); // 多边形基准 8×8
            bool hot = overlay.GripActive == i;
            handle.Color = hot ? new Color(1f, 1f, 1f, 0.85f) : new Color(1f, 1f, 1f, 0.32f);
        }

        // 拖角：轮询鼠标（和整排拖动同一套），按下点距角 <= 16 px 才算抓住
        Vector2 mouse = overlay.Root.GetGlobalMousePosition();
        bool pressed = Input.IsMouseButtonPressed(MouseButton.Left);

        // 平时不显示：鼠标在球排区域内停留满 1 s 才淡出框来；离开立刻收起（拖动期间常显）
        Vector2 centreScreen = overlay.Root.ToGlobal(new Vector2(0f, centreY));
        float halfScreenW = halfW * scale;
        float halfScreenH = halfH * scale;
        bool inside = overlay.GripActive >= 0
                      || (Mathf.Abs(mouse.X - centreScreen.X) <= halfScreenW + 28f
                          && Mathf.Abs(mouse.Y - centreScreen.Y) <= halfScreenH + 28f);

        if (inside)
        {
            overlay.GripHoverT = Mathf.Min(1.2f, overlay.GripHoverT + delta);
        }
        else
        {
            overlay.GripHoverT = 0f;
        }

        bool showGrip = overlay.GripActive >= 0 || overlay.GripHoverT >= 1f;
        overlay.GripRoot.Visible = showGrip;

        if (!showGrip)
        {
            overlay.GripLatch = pressed;
            return;
        }

        if (overlay.GripActive < 0)
        {
            if (pressed && !overlay.GripLatch)
            {
                for (int i = 0; i < 4; i++)
                {
                    if ((mouse - overlay.Root.ToGlobal(corners[i])).Length() > 16f)
                    {
                        continue;
                    }

                    overlay.GripActive = i;
                    overlay.GripAnchorScreen = overlay.Root.ToGlobal(new Vector2(0f, centreY));
                    overlay.GripStartScale = scale;
                    overlay.GripStartDist = Mathf.Max(1f, (mouse - overlay.GripAnchorScreen).Length());
                    break;
                }
            }
        }
        else if (!pressed)
        {
            overlay.GripActive = -1;
            Config.Save();
            GD.Print($"[BangDreamInSpire/handpips] free scale saved {Config.Data.FreeScale:F2}");
        }
        else
        {
            float ratio = Mathf.Max(1f, (mouse - overlay.GripAnchorScreen).Length()) / overlay.GripStartDist;
            float next = Mathf.Clamp(overlay.GripStartScale * ratio, 0.5f, 2.5f);
            // 节点实际缩放是 next / FreeParentScale，位移换算必须用同一个系数，否则框心会漂
            float applied = next / Mathf.Max(0.05f, overlay.FreeParentScale);
            Config.Data.FreeScale = next;
            Config.Data.FreeX = overlay.GripAnchorScreen.X;
            Config.Data.FreeY = overlay.GripAnchorScreen.Y - centreY * applied;
        }

        overlay.GripLatch = pressed;
    }

    private static Line2D MakeGripEdge(Node2D parent)
    {
        var line = new Line2D
        {
            Width = 1.2f,
            DefaultColor = new Color(1f, 1f, 1f, 0.38f),
            Antialiased = false,
        };
        parent.AddChild(line);
        return line;
    }

    /// <summary>
    /// 父链（手牌及其容器）的累积缩放。自由摆放只在进入时取一次：
    /// TopLevel 之后 Root.Scale 不再被父缩放乘一遍，用这个值反算一次，开关瞬间视觉尺寸不变。
    /// </summary>
    private static float ParentGlobalScale(Node2D node)
    {
        try
        {
            if (node.GetParent() is CanvasItem parent)
            {
                Vector2 scale = parent.GetGlobalTransform().Scale;
                float k = Mathf.Max(Mathf.Abs(scale.X), Mathf.Abs(scale.Y));
                if (k > 0.05f)
                {
                    return k;
                }
            }
        }
        catch
        {
            // 拿不到父变换就按 1 处理
        }

        return 1f;
    }

    private static void HideGrip(Overlay overlay)
    {
        if (overlay.GripRoot != null && GodotObject.IsInstanceValid(overlay.GripRoot))
        {
            overlay.GripRoot.Visible = false;
        }

        overlay.GripActive = -1;
        overlay.GripLatch = false;
    }


    /// <summary>安全挂父：父节点无效时把子节点排掉，避免"无父又无引用"的 native 节点泄漏。</summary>
    private static bool TryAttach(Node parent, Node child)
    {
        if (parent == null || !GodotObject.IsInstanceValid(parent) || parent.IsQueuedForDeletion())
        {
            if (GodotObject.IsInstanceValid(child))
            {
                child.QueueFree();
            }

            return false;
        }

        try
        {
            parent.AddChild(child);
            return true;
        }
        catch
        {
            if (GodotObject.IsInstanceValid(child))
            {
                child.QueueFree();
            }

            return false;
        }
    }
}
