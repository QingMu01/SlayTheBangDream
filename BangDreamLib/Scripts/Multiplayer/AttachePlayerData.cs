using BangDreamLib.Scripts.Mechanics;
using BangDreamLib.Scripts.Mechanics.Lingered;
using BangDreamLib.Scripts.Mechanics.MusicNote;
using BangDreamLib.Scripts.Mechanics.Perform;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Utils;

namespace BangDreamLib.Scripts.Multiplayer;

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

    /// <summary>演奏机制状态；未启用演奏机制的角色访问会抛出异常。</summary>
    public PerformManager PerformManager => Get<PerformManager>();

    /// <summary>余音环绕预览状态；未启用余音机制的角色访问会抛出异常。</summary>
    public LingeredOrbitManager LingeredOrbitManager => Get<LingeredOrbitManager>();

    /// <summary>音符伤害追踪状态；未启用音符机制的角色访问会抛出异常。</summary>
    public MusicNoteDamageTracker MusicNoteDamageTracker => Get<MusicNoteDamageTracker>();

    private T Get<T>() where T : AbstractModel
    {
        return _models.TryGetValue(typeof(T), out var model)
            ? (T)model
            : throw new InvalidOperationException($"机制状态 '{typeof(T).Name}' 未注册到当前玩家。");
    }
}
