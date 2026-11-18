using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Mechanics.Perform;
using BangDreamInSpire.Scripts.Mechanics.Puppeteer;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamInSpire.Scripts.Console;

/// <summary>
/// 调试命令：傀儡师形态与歌单冻结的验收入口。
/// <para>
/// 用法：
/// <c>puppet state</c> 打印双生命池 / 形态 / 激活位；
/// <c>puppet kill</c> 直接击杀当前登场形态（小睦形态会切墨缇丝，墨缇丝形态会真的败北，慎用）；
/// <c>puppet activate CDFG</c> 手动激活分组（走与"试图演奏"相同的入口，四组齐了会复活小睦）；
/// <c>puppet freeze</c> / <c>puppet unfreeze</c> 单独验收冻结表现。
/// </para>
/// <para>
/// 仅单人 / 主机验收用：命令声明 <c>IsNetworked = false</c>，不会广播；多人局里由任一端单独执行会造成
/// host/client 状态分歧，请只在单人或主机侧验收。
/// </para>
/// </summary>
public class PuppeteerConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "puppet";

    public override string Args => "<state | kill | activate CDFG | freeze | unfreeze>";

    public override string Description => "dev: 傀儡师形态 / 分组激活 / 歌单冻结的调试入口";

    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (issuingPlayer == null)
        {
            return new CmdResult(false, "需要在战斗中使用（无玩家上下文）。");
        }

        if (issuingPlayer.Character is not IPuppeteerCharacter)
        {
            return new CmdResult(false, "当前角色不是傀儡师角色。");
        }

        var manager = issuingPlayer.AttachedData().PuppeteerFormManager;
        var performManager = issuingPlayer.AttachedData().PerformManager;
        var action = args.Length > 0 ? args[0].ToLowerInvariant() : "state";

        switch (action)
        {
            case "state":
                var data = PuppeteerRunState.Get(issuingPlayer);
                return new CmdResult(true,
                    $"形态 {manager.ActiveForm} | Creature {issuingPlayer.Creature.CurrentHp}/{issuingPlayer.Creature.MaxHp} | " +
                    $"小睦 {data.HostHp}/{data.HostMaxHp} | 墨缇丝 {data.PuppetHp}/{data.PuppetMaxHp} | " +
                    $"激活 {manager.ActivatedGroups} | 冻结 {performManager.IsPlaylistFrozen} | " +
                    $"歌单容量 {performManager.Capacity}");

            case "kill":
                TaskHelper.RunSafely(CreatureCmd.Kill(issuingPlayer.Creature));
                return new CmdResult(true, $"已对当前形态结算死亡（形态 {manager.ActiveForm}）。");

            case "activate":
                if (args.Length < 2 || !TryParseChords(args[1], out var groups))
                {
                    return new CmdResult(false, "用法：puppet activate CDFG（C/D/F/G 任意组合）。");
                }

                if (manager.ActiveForm != PuppeteerForm.Puppet)
                {
                    return new CmdResult(false, "只有墨缇丝形态才会累积分组激活。");
                }

                manager.ActivateGroups(groups);
                return new CmdResult(true, $"已激活 {groups}，当前共 {manager.ActivatedGroups}。");

            case "freeze":
                performManager.SetPlaylistFrozen(true);
                return new CmdResult(true, "歌单已冻结。");

            case "unfreeze":
                performManager.SetPlaylistFrozen(false);
                return new CmdResult(true, "歌单已解冻。");

            default:
                return new CmdResult(false, "未知子命令。用 state / kill / activate / freeze / unfreeze。");
        }
    }

    private static bool TryParseChords(string text, out PerformChord chord)
    {
        chord = PerformChord.None;
        foreach (var letter in text.ToUpperInvariant())
        {
            var parsed = letter switch
            {
                'C' => PerformChord.C,
                'D' => PerformChord.D,
                'F' => PerformChord.F,
                'G' => PerformChord.G,
                _ => PerformChord.None
            };
            if (parsed == PerformChord.None)
            {
                chord = PerformChord.None;
                return false;
            }

            chord |= parsed;
        }

        return chord != PerformChord.None;
    }
}
