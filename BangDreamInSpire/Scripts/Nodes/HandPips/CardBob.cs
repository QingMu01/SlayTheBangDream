using System;
using Godot;

namespace BangDreamInSpire.Scripts.Nodes.HandPips;

/// <summary>
/// 卡面里 "Head" 节点的查找（避让探头用）。
/// 实况：探头小人交还给角色 mod 自己处理，原先这一排的探头动画（CardBob.Tick：显式平移 +
/// progress 同步 + 每张卡的 State 表）已经停用并从本类删除，这里只剩一个纯查找工具，
/// 供 OrbRenderer.FindHeadCached 按卡牌实例缓存调用。
/// </summary>
internal static class CardBob
{
    internal static TextureRect FindHead(Node node, int depth)
    {
        if (node == null || depth > 4)
        {
            return null;
        }

        foreach (Node child in node.GetChildren(false))
        {
            if (child is TextureRect rect && rect.Name.ToString().Equals("Head", StringComparison.OrdinalIgnoreCase))
            {
                return rect;
            }

            TextureRect nested = FindHead(child, depth + 1);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
