using MegaCrit.Sts2.Core.Entities.Players;

namespace BangDreamLib.Scripts.Interfaces.GameHook;

public interface ILingeredChangedHook
{
    Task AfterLingeredEnergyAdded(Player player, int amount)
    {
        return Task.CompletedTask;
    }

    Task AfterLingeredEnergyReduced(Player player, int amount)
    {
        return Task.CompletedTask;
    }

    Task<bool> OnLingeredEnergyFilled(Player player, int amount)
    {
        return Task.FromResult(false);
    }
    
    Task FinalUnprocessedOverflow(Player player, int amount)
    {
        return Task.CompletedTask;
    }
}