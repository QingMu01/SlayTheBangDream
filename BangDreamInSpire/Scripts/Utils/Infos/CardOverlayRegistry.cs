using System;
using System.Collections.Generic;
using Godot;
using STS2RitsuLib.Models.Capabilities;

namespace BangDreamInSpire.Scripts.Utils.Infos;

/// <summary>
/// 卡面覆盖层工厂注册表。由 <c>Nodes</c> 侧的机制在初始化时注册工厂，<c>Mechanics</c> 侧只按 ID 解析。
/// </summary>
public static class CardOverlayRegistry
{
    private static readonly Dictionary<string, ICardOverlayFactory> Factories = [];

    /// <summary>注册或替换一个覆盖层工厂。</summary>
    public static void Register(string id, ICardOverlayFactory factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Factories[id] = factory;
    }

    /// <summary>解析覆盖层工厂；未注册时返回 <see langword="null" />。</summary>
    public static Func<CardOverlayContext, Control?>? Resolve(string id)
    {
        return Factories.TryGetValue(id, out var factory)
            ? ctx => factory.Create(ctx.Card)
            : null;
    }
}
