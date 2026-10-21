using System.Text;
using BangDreamInSpire.Scripts.Nodes.MainMenu;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace BangDreamInSpire.Scripts.Console;

/// <summary>
/// 主菜单角色点击动画的调试指令：查看 / 切换手感参数组。
/// 只改本次运行的内存状态，不写盘——重启即回到随机。
/// </summary>
public sealed class ClickProfileConsoleCmd : AbstractConsoleCmd
{
    private const string RandomKeyword = "random";
    private const string ListKeyword = "list";

    public override string CmdName => "bdclick";

    public override string Args => "list | <序号 1-N> | <参数组名> | random";

    public override string Description => "查看/切换主菜单角色点击动画的手感参数组";

    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length == 0 || IsKeyword(args[0], ListKeyword))
            return new CmdResult(true, DescribeProfiles());

        var argument = args[0].Trim();
        if (IsKeyword(argument, RandomKeyword))
        {
            MainMenuEnvironmentCharacter.ClearForcedClickProfile();
            return new CmdResult(true, $"已恢复随机。\n{DescribeProfiles()}");
        }

        var names = MainMenuEnvironmentCharacter.ClickProfileNames;
        var index = int.TryParse(argument, out var oneBased)
            ? oneBased - 1
            : Array.FindIndex(names, name => name.Equals(argument, StringComparison.OrdinalIgnoreCase));

        if (!MainMenuEnvironmentCharacter.TryForceClickProfile(index))
            return new CmdResult(false, $"没有第 {argument} 组手感。\n{DescribeProfiles()}");

        return new CmdResult(true, $"已切换到：{names[index]}\n{DescribeProfiles()}");
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args)
    {
        string[] candidates = [.. MainMenuEnvironmentCharacter.ClickProfileNames, RandomKeyword, ListKeyword];
        var completed = args.Length <= 1 ? [] : args[..^1];
        var partial = args.Length == 0 ? string.Empty : args[^1];
        return CompleteArgument(candidates, completed, partial, CompletionType.Subcommand);
    }

    private static string DescribeProfiles()
    {
        var names = MainMenuEnvironmentCharacter.ClickProfileNames;
        var details = MainMenuEnvironmentCharacter.ClickProfileDetails;
        var forcedIndex = MainMenuEnvironmentCharacter.ForcedClickProfileIndex;
        var activeName = MainMenuEnvironmentCharacter.CurrentClickProfileName;
        var activeIndex = activeName == null ? -1 : Array.IndexOf(names, activeName);

        var builder = new StringBuilder();
        builder.Append("当前手感：");
        if (forcedIndex is { } forced)
            builder.Append($"{names[forced]}（第 {forced + 1} 组，本次运行内强制指定）");
        else if (activeName != null)
            builder.Append($"{activeName}（进入主菜单时随机）");
        else
            builder.Append("无角色实例，设置将在下次进入主菜单生效");

        builder.Append('\n');
        for (var i = 0; i < names.Length; i++)
            builder.Append(i == activeIndex ? "> " : "  ")
                .Append(i + 1).Append(". ").Append(names[i]).Append("  ").Append(details[i]).Append('\n');

        builder.Append("用法：bdclick <序号|名字|random>");
        return builder.ToString();
    }

    private static bool IsKeyword(string value, string keyword) =>
        value.Trim().Equals(keyword, StringComparison.OrdinalIgnoreCase);
}
