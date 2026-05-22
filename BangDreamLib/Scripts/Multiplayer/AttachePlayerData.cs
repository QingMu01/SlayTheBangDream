using BangDreamLib.Scripts.Features;
using BangDreamLib.Scripts.Features.Tracker;
using BangDreamLib.Scripts.Saved;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Utils;

namespace BangDreamLib.Scripts.Multiplayer;

public class AttachePlayerData
{
    public static readonly AttachedState<Player, AttachePlayerData>
        State = new(player => new AttachePlayerData(player));

    public SkinManager SkinManager { get; private set; }
    public LingeredEnergyCounter LingeredEnergy { get; }
    public MusicNoteDamageTracker MusicNoteDamageTracker { get; }

    private AttachePlayerData(Player player)
    {
        SkinManager = new SkinManager(player);
        LingeredEnergy = (LingeredEnergyCounter)ModelDb.Singleton<LingeredEnergyCounter>().MutableClone();
        MusicNoteDamageTracker = (MusicNoteDamageTracker)ModelDb.Singleton<MusicNoteDamageTracker>().MutableClone();

        LingeredEnergy.Owner = player;
    }

    public void SetMultiplayerSkin(Player player, SavedSkin savedSkin)
    {
        SkinManager = new SkinManager(player, savedSkin);
    }
}