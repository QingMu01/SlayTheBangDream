using System;
using System.IO;

namespace BangDreamInSpire.Scripts.Nodes.HandPips;

/// <summary>
/// 移植自「更直观的手牌 · 精简版」时用来顶替它原来的 ModEntry：
/// mod id 用 BangDreamInSpire（设置页因此挂在依赖项下面），目录用本程序集所在目录。
/// </summary>
internal static class ModEntry
{
    public const string ModId = "BangDreamInSpire";

    public static string ModDir { get; } =
        Path.GetDirectoryName(typeof(ModEntry).Assembly.Location) ?? ".";

    public static void ApplyLive() => OrbRenderer.InvalidatePlacement();
}
