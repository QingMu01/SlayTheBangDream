using BangDreamInSpire.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Players;
using STS2RitsuLib.RunData;

namespace BangDreamInSpire.Scripts.Mechanics.Puppeteer;

/// <summary>当前形态。</summary>
public enum PuppeteerForm
{
    /// <summary>本体形态。</summary>
    Host = 0,

    /// <summary>傀儡形态。</summary>
    Puppet = 1
}

/// <summary>每玩家跨战斗的双生命池与形态。</summary>
public sealed class PuppeteerRunData
{
    /// <summary>是否已用角色初始值初始化。</summary>
    public bool Initialized { get; set; }

    public int HostHp { get; set; }

    public int HostMaxHp { get; set; }

    public int PuppetHp { get; set; }

    public int PuppetMaxHp { get; set; }

    /// <summary>当前形态，战斗外恒为 Host。</summary>
    public PuppeteerForm ActiveForm { get; set; } = PuppeteerForm.Host;
}

/// <summary>傀儡师运行数据入口。</summary>
public static class PuppeteerRunState
{
    public const string RunDataKey = "PuppeteerState";

    private static PlayerRunSavedData<PuppeteerRunData>? _data;

    /// <summary>每玩家运行数据访问器。</summary>
    public static PlayerRunSavedData<PuppeteerRunData> Data =>
        _data ?? throw new InvalidOperationException(
            "PlayerRunSavedData.PuppeteerState is not BangDreamTools.Initialized.");

    internal static void Bind(PlayerRunSavedData<PuppeteerRunData> data)
    {
        BangDreamTools.Init(ref _data, data, nameof(Data));
    }

    /// <summary>读取或创建玩家双池数据。</summary>
    public static PuppeteerRunData Get(Player player)
    {
        return Data.Get(player);
    }

    /// <summary>修改双池数据并同步大厅。</summary>
    public static void Modify(Player player, Action<PuppeteerRunData> mutate)
    {
        Data.Modify(player, mutate);
    }
}
