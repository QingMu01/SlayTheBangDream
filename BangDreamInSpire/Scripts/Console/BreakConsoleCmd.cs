using BangDreamInSpire.Scripts.Mechanics.Break;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamInSpire.Scripts.Console;

/// <summary>break [amount|clear|self amount] 调试破绽；仅单人或主机使用。</summary>
public class BreakConsoleCmd : AbstractConsoleCmd
{
    private const string ClearKeyword = "clear";
    private const string SelfKeyword = "self";

    public override string CmdName => "break";

    public override string Args => "<层数 1-4 | clear> [self]";

    public override string Description => "dev: 对敌人（或 self 自己）发现破绽，验收弱点击破与常驻视觉";

    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (issuingPlayer == null)
        {
            return new CmdResult(false, "需要在战斗中使用（无玩家上下文）。");
        }

        var combatState = issuingPlayer.Creature.CombatState;
        if (combatState == null || !combatState.IsLiveCombat())
        {
            return new CmdResult(false, "需要在战斗中使用。");
        }

        var self = args.Any(arg => arg.Equals(SelfKeyword, StringComparison.OrdinalIgnoreCase));
        var targets = self
            ? [issuingPlayer.Creature]
            : combatState.Enemies.Where(enemy => enemy.IsHittable && !enemy.IsDead).ToArray();
        if (targets.Length == 0)
        {
            return new CmdResult(false, "没有可施放的目标。");
        }

        var isClear = args.Any(arg => arg.Equals(ClearKeyword, StringComparison.OrdinalIgnoreCase));
        if (isClear)
        {
            TaskHelper.RunSafely(ClearAsync(targets));
            return new CmdResult(true, $"已清空 {targets.Length} 个目标的破绽。");
        }

        if (!TryParseAmount(args, out var amount))
        {
            return new CmdResult(false, "层数需为 1-4 的整数；或用 break clear 清空。");
        }

        TaskHelper.RunSafely(DiscoverAsync(issuingPlayer, targets, amount));
        return new CmdResult(true, $"对 {targets.Length} 个目标发现 {amount} 个破绽。");
    }

    private static bool TryParseAmount(string[] args, out int amount)
    {
        amount = 0;
        foreach (var arg in args)
        {
            if (arg.Equals(SelfKeyword, StringComparison.OrdinalIgnoreCase)) continue;
            if (!int.TryParse(arg, out amount)) return false;
            return amount is >= 1 and <= BreakConst.MaxStacks;
        }

        return false;
    }

    private static async Task DiscoverAsync(Player player, IReadOnlyList<Creature> targets, int amount)
    {
        var choiceContext = new ThrowingPlayerChoiceContext();
        foreach (var target in targets)
        {
            await BreakCmd.Discover(choiceContext, target, amount, null);
            await Cmd.CustomScaledWait(0.1f, 0.2f);
        }
    }

    private static async Task ClearAsync(IReadOnlyList<Creature> targets)
    {
        foreach (var target in targets)
        {
            await PowerCmd.Remove<WeakPointBreakPower>(target);
            await PowerCmd.Remove<WeaknessBreakPower>(target);
        }
    }
}
