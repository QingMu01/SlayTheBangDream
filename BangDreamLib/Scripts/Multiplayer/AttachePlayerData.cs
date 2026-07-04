using BangDreamLib.Scripts.Features;
using BangDreamLib.Scripts.Features.Tracker;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Utils;

namespace BangDreamLib.Scripts.Multiplayer;

public class AttachePlayerData
{
    public static readonly AttachedState<Player, AttachePlayerData> State = new(p => new AttachePlayerData(p));

    public PerformanceManager PerformanceManager { get; }
    public MusicNoteDamageTracker MusicNoteDamageTracker { get; }

    private AttachePlayerData(Player player)
    {
        PerformanceManager = (PerformanceManager)ModelDb.Singleton<PerformanceManager>().MutableClone();

        MusicNoteDamageTracker = (MusicNoteDamageTracker)ModelDb.Singleton<MusicNoteDamageTracker>().MutableClone();

        PerformanceManager.Player = player;
    }
}