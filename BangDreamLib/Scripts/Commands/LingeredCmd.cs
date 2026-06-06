using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Features;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Commands;

public static class LingeredCmd
{
    public static async Task JustAdd(Player player, int amount)
    {
        ArgumentNullException.ThrowIfNull(player.Creature.CombatState);
        await player.AttachedData().LingeredEnergy.AddLingeredEnergy(player.Creature.CombatState, amount);
    }

    public static async Task JustReduce(Player player, int amount)
    {
        ArgumentNullException.ThrowIfNull(player.Creature.CombatState);
        await player.AttachedData().LingeredEnergy.ReduceLingeredEnergy(player.Creature.CombatState, amount);
    }

    public static async Task AddLeByCard(CardModel card, int amount)
    {
        var context = card.Owner.AttachedData().LingeredEnergy;
        if (card.CombatState != null)
        {
            await DoAddEnergy(context, card.CombatState, amount);
        }
    }

    public static async Task ReduceLeByCard(CardModel card, int amount)
    {
        var context = card.Owner.AttachedData().LingeredEnergy;
        if (card.CombatState != null)
        {
            await DoReduceEnergy(context, card.CombatState, amount);
        }
    }

    public static async Task AddLeByPower(PowerModel power, int amount)
    {
        if (power.Owner.Player != null)
        {
            var context = power.Owner.Player.AttachedData().LingeredEnergy;
            await DoAddEnergy(context, power.CombatState, amount);
        }
    }

    public static async Task ReduceLeByPower(PowerModel power, int amount)
    {
        if (power.Owner.Player != null)
        {
            var context = power.Owner.Player.AttachedData().LingeredEnergy;
            await DoReduceEnergy(context, power.CombatState, amount);
        }
    }

    public static async Task AddLeByRelic(RelicModel relic, int amount)
    {
        var context = relic.Owner.AttachedData().LingeredEnergy;
        if (relic.Owner.Creature.CombatState != null)
        {
            await DoAddEnergy(context, relic.Owner.Creature.CombatState, amount);
        }
    }

    public static async Task ReduceLeByRelic(RelicModel relic, int amount)
    {
        var context = relic.Owner.AttachedData().LingeredEnergy;
        if (relic.Owner.Creature.CombatState != null)
        {
            await DoReduceEnergy(context, relic.Owner.Creature.CombatState, amount);
        }
    }

    private static async Task DoAddEnergy(LingeredEnergyCounter lec, ICombatState combatState, int amount)
    {
        var finalAdded = BangDreamHook.ModifyLingeredEnergyAdd(combatState, amount);
        if (finalAdded <= 0) return;
        await lec.AddLingeredEnergy(combatState, (int)finalAdded);
    }

    private static async Task DoReduceEnergy(LingeredEnergyCounter lec, ICombatState combatState, int amount)
    {
        var finalReduced = BangDreamHook.ModifyLingeredEnergyReduce(combatState, amount);
        if (finalReduced <= 0) return;
        await lec.ReduceLingeredEnergy(combatState, (int)finalReduced);
    }
}