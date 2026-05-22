using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using STS2RitsuLib.CardPiles;

namespace BangDreamLib.Scripts.Extensions;

public static class CustomPileExtensions
{
    public static PileType GetPileType(this string pileId)
    {
        return ModCardPileRegistry.GetPileType(pileId);
    }

    public static CardPile GetPile(this string pileId, Player player)
    {
        var pileType = ModCardPileRegistry.GetPileType(pileId);
        return CardPile.Get(pileType, player) ??
               throw new InvalidOperationException($"Player {player.Creature.Name} does not have pile type {pileType}");
    }
}