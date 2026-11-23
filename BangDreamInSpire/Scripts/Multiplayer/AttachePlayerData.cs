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

/// <summary>装配机制声明的玩家状态。</summary>
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

    /// <summary>演奏服务。</summary>
    public IPerformService Perform => Get<PerformManager>();

    /// <summary>演奏管理器。</summary>
    internal PerformManager PerformManager => Get<PerformManager>();

    /// <summary>余音环绕管理器。</summary>
    internal LingeredOrbitManager LingeredOrbitManager => Get<LingeredOrbitManager>();

    /// <summary>音符伤害服务。</summary>
    public IMusicNoteTracker MusicNoteTracker => Get<MusicNoteDamageTracker>();

    /// <summary>音符伤害追踪器。</summary>
    internal MusicNoteDamageTracker MusicNoteDamageTracker => Get<MusicNoteDamageTracker>();

    /// <summary>傀儡师公共状态。</summary>
    public IPuppeteerState Puppeteer => Get<PuppeteerFormManager>();

    /// <summary>傀儡师形态管理器。</summary>
    internal PuppeteerFormManager PuppeteerFormManager => Get<PuppeteerFormManager>();

    private T Get<T>() where T : AbstractModel
    {
        return _models.TryGetValue(typeof(T), out var model)
            ? (T)model
            : throw new InvalidOperationException($"机制状态 '{typeof(T).Name}' 未注册到当前玩家。");
    }
}
