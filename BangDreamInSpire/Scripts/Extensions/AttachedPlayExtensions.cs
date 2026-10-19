using BangDreamInSpire.Scripts.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;

namespace BangDreamInSpire.Scripts.Extensions;

public static class AttachedPlayExtensions
{
    public static AttachePlayerData AttachedData(this Player player)
    {
        return AttachePlayerData.State.GetOrCreate(player);
    }
}