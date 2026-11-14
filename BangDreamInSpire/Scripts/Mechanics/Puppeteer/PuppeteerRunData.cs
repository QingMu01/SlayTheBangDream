using BangDreamInSpire.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Players;
using STS2RitsuLib.RunData;

namespace BangDreamInSpire.Scripts.Mechanics.Puppeteer;

/// <summary>当前登场形态。</summary>
public enum PuppeteerForm
{
    /// <summary>本体形态（纪录中的"小睦"）。战斗外只允许处于本形态。</summary>
    Host = 0,

    /// <summary>傀儡形态（纪录中的"墨缇丝"）。仅在本体阵亡后于战斗内登场。</summary>
    Puppet = 1
}

/// <summary>
/// 傀儡师的跨战斗运行数据：两组生命池 + 当前形态。
/// <para>
/// 通过 <c>RunData.RegisterPerPlayer("PuppeteerState")</c> 注册，每名玩家一份，
/// 因此多人各玩家的双池互不干扰。本体 <see cref="MegaCrit.Sts2.Core.Entities.Creatures.Creature" />
/// 的 HP 是「当前登场形态」的生命，切形态时写回这里保存。
/// </para>
/// </summary>
public sealed class PuppeteerRunData
{
    /// <summary>是否已经从角色与生物初始值完成过一次初始化。默认工厂无法拿到玩家，故由管理者惰性初始化。</summary>
    public bool Initialized { get; set; }

    /// <summary>本体形态当前生命。</summary>
    public int HostHp { get; set; }

    /// <summary>本体形态最大生命。</summary>
    public int HostMaxHp { get; set; }

    /// <summary>傀儡形态当前生命。</summary>
    public int PuppetHp { get; set; }

    /// <summary>傀儡形态最大生命。</summary>
    public int PuppetMaxHp { get; set; }

    /// <summary>当前形态。战斗外恒为 <see cref="PuppeteerForm.Host" />。</summary>
    public PuppeteerForm ActiveForm { get; set; } = PuppeteerForm.Host;
}

/// <summary>
/// <see cref="PuppeteerRunData" /> 的静态入口。写法对齐 <c>BangDreamConst.ExtraCardMerchant</c>，
/// 但归本机制所有，因此不放进 <c>BangDreamConst</c>（核心常量文件是跨机制的公共面）。
/// </summary>
public static class PuppeteerRunState
{
    /// <summary>运行数据槽位键。</summary>
    public const string RunDataKey = "PuppeteerState";

    private static PlayerRunSavedData<PuppeteerRunData>? _data;

    /// <summary>本机制的每玩家运行数据访问器；内容注册完成前访问会抛异常。</summary>
    public static PlayerRunSavedData<PuppeteerRunData> Data =>
        _data ?? throw new InvalidOperationException(
            "PlayerRunSavedData.PuppeteerState is not BangDreamTools.Initialized.");

    internal static void Bind(PlayerRunSavedData<PuppeteerRunData> data)
    {
        BangDreamTools.Init(ref _data, data, nameof(Data));
    }

    /// <summary>取玩家的双池数据；不存在时创建默认值。</summary>
    public static PuppeteerRunData Get(Player player)
    {
        return Data.Get(player);
    }

    /// <summary>修改玩家的双池数据并标记槽位已变更（会按 <c>SyncLobbyOnChange</c> 同步大厅）。</summary>
    public static void Modify(Player player, Action<PuppeteerRunData> mutate)
    {
        Data.Modify(player, mutate);
    }
}
