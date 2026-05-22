using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Features;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace BangDreamLib.Scripts.Console;

public class LingeredConsoleCmd : AbstractConsoleCmd
{
    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (!CombatManager.Instance.IsInProgress)
            return new CmdResult(false, "This doesn't appear to be a combat!");
        if (issuingPlayer?.PlayerCombatState == null || issuingPlayer.Creature.CombatState == null)
        {
            return new CmdResult(false, "who issuing command???");
        }

        var playerData = issuingPlayer.AttachedData();

        var length = args.Length;
        if (length <= 1)
        {
            return new CmdResult(true,
                $"Your LingeredEnergy counter is {playerData.LingeredEnergy.Counter}.");
        }

        if (args[0].ToLower().Equals("add"))
        {
            if (!int.TryParse(args[1], out var amount))
                return new CmdResult(false, "Invalid amount!");
            if (amount < 0)
            {
                amount = 0;
            }
            else if (amount > 999)
            {
                amount = 999;
            }

            return new CmdResult(
                OptionLeAddCondition(playerData.LingeredEnergy, issuingPlayer.Creature.CombatState, amount),
                true,
                $"added {amount} LingeredEnergy.");
        }

        if (args[0].ToLower().Equals("reduce"))
        {
            if (!int.TryParse(args[1], out var amount))
                return new CmdResult(false, "Invalid amount!");
            if (amount < 0)
            {
                amount = 0;
            }
            else if (amount > playerData.LingeredEnergy.Counter)
            {
                amount = playerData.LingeredEnergy.Counter;
            }

            return new CmdResult(
                OptionLeReduceCondition(playerData.LingeredEnergy, issuingPlayer.Creature.CombatState, amount), true,
                $"reduced {amount} LingeredEnergy.");
        }

        return new CmdResult(false, "Invalid option!");
    }

    public override string CmdName => "lingered";
    public override string Args => "<options:string> [effect:int]";
    public override string Description => "modify(add,reduce) your LingeredEnergy. or get your LingeredEnergy counter.";
    public override bool IsNetworked => true;


    private static async Task OptionLeAddCondition(LingeredEnergyCounter context, ICombatState combatState, int amount)
    {
        await context.AddLingeredEnergy(combatState, amount);
    }

    private static async Task OptionLeReduceCondition(LingeredEnergyCounter context, ICombatState combatState,
        int amount)
    {
        await context.ReduceLingeredEnergy(combatState, amount);
    }
}