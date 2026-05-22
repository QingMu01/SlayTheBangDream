using BangDreamLib.Scripts.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;

namespace BangDreamLib.Scripts.Extensions;

public static class AttachedPlayExtensions
{
    public static AttachePlayerData AttachedData(this Player player)
    {
        return AttachePlayerData.State.GetOrCreate(player);
    }

    public static AttachePlayerNode AttachedNode(this Player player)
    {
        return AttachePlayerNode.State.GetOrCreate(player.Creature.GetCreatureNode()!);
    }
}