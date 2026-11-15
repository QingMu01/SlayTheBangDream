using BangDreamInSpire.Scripts.Mechanics;
using BangDreamInSpire.Scripts.Mechanics.Lingered;
using BangDreamInSpire.Scripts.Mechanics.MusicNote;
using BangDreamInSpire.Scripts.Interfaces.Mechanics;
using BangDreamInSpire.Scripts.Mechanics.Perform;
using BangDreamInSpire.Scripts.Mechanics.Puppeteer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Utils;

namespace BangDreamInSpire.Scripts.Multiplayer;

/// <summary>
/// 玩家附加状态：按各机制声明的玩家状态模型构建实例。
/// 新增机制只需在对应 <see cref="IBangDreamMechanic" /> 中实现
/// <see cref="IBangDreamMechanic.InstantiatePlayerState" />，无需修改本类。
/// </summary>
public class AttachePlayerData
{
    public static readonly AttachedState<Player, AttachePlayerData> State = new(p => new AttachePlayerData(p));

    private readonly Dictionary<Type, AbstractModel> _models = [];

    public AttachePlayerData(Player player)
    {
        foreach (var model in BangDreamMechanicRegistry.InstantiatePlayerStates(player))
        {
            _models[model.GetType()] = model;
        }
    }

    /// <summary>演奏机制服务；未启用演奏机制的角色访问会抛出异常。</summary>
    public IPerformService Perform => Get<PerformManager>();

    /// <summary>演奏机制状态（核心内部使用）；未启用演奏机制的角色访问会抛出异常。</summary>
    internal PerformManager PerformManager => Get<PerformManager>();

    /// <summary>余音环绕预览状态（核心内部使用）；未启用余音机制的角色访问会抛出异常。</summary>
    internal LingeredOrbitManager LingeredOrbitManager => Get<LingeredOrbitManager>();

    /// <summary>音符伤害统计服务；未启用音符机制的角色访问会抛出异常。</summary>
    public IMusicNoteTracker MusicNoteTracker => Get<MusicNoteDamageTracker>();

    /// <summary>音符伤害追踪状态（核心内部使用）；未启用音符机制的角色访问会抛出异常。</summary>
    internal MusicNoteDamageTracker MusicNoteDamageTracker => Get<MusicNoteDamageTracker>();

    /// <summary>傀儡师形态状态；未启用傀儡师机制的角色访问会抛出异常。</summary>
    public IPuppeteerState Puppeteer => Get<PuppeteerFormManager>();

    /// <summary>傀儡师形态状态（核心内部使用）；未启用傀儡师机制的角色访问会抛出异常。</summary>
    internal PuppeteerFormManager PuppeteerFormManager => Get<PuppeteerFormManager>();

    private T Get<T>() where T : AbstractModel
    {
        return _models.TryGetValue(typeof(T), out var model)
            ? (T)model
            : throw new InvalidOperationException($"机制状态 '{typeof(T).Name}' 未注册到当前玩家。");
    }
}
