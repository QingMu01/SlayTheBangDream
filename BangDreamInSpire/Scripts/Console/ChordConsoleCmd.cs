using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Mechanics.Perform.Chord;
using HarmonyLib;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Cards;
using STS2RitsuLib.Models.Capabilities;

namespace BangDreamInSpire.Scripts.Console;

/// <summary>
/// 调试命令：给手牌设置演奏和弦，用来预览卡牌右上角和弦角标的切分效果。
/// <para>
/// 用法：<c>chord CDF</c>（和弦字母任意组合）、<c>chord 13</c>（位掩码，C=1/D=2/F=4/G=8）、
/// <c>chord none</c>（清空）。卡牌角标由能力的卡面覆盖层绘制，只在
/// <c>NCard.ReloadOverlay</c> 之后重建，因此改完和弦要手动触发一次覆盖层刷新。
/// </para>
/// </summary>
public class ChordConsoleCmd : AbstractConsoleCmd
{
    private const string NoneKeyword = "none";

    /// <summary>本体 <c>NCard.ReloadOverlay</c> 是私有方法，这里委派成委托调用它重建能力覆盖层。</summary>
    private static readonly Action<NCard>? ReloadOverlay = AccessTools.DeclaredMethod(typeof(NCard), "ReloadOverlay")
        is { } method
        ? AccessTools.MethodDelegate<Action<NCard>>(method)
        : null;

    public override string CmdName => "chord";

    public override string Args => "<C|D|F|G 组合 / 位掩码 1/2/4/8 / none>";

    public override string Description => "dev: 给手牌设置演奏和弦，预览右上角和弦角标";

    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (issuingPlayer == null)
        {
            return new CmdResult(false, "需要在战斗中使用（无玩家上下文）。");
        }

        var text = args.Length > 0 ? args[0] : string.Empty;
        if (!TryParseChord(text, out var chord))
        {
            return new CmdResult(false, $"无法识别的和弦「{text}」：用 C/D/F/G 组合、位掩码 1/2/4/8 或 none。");
        }

        var cards = PileType.Hand.GetPile(issuingPlayer).Cards.ToList();
        foreach (var card in cards)
        {
            if (chord == PerformChord.None)
            {
                if (card.TryGetCapability<ChordCapability>(out var capability))
                {
                    capability.SetChord(PerformChord.None);
                }
            }
            else
            {
                card.GetOrCreateCapability<ChordCapability>().SetChord(chord);
            }

            if (NCard.FindOnTable(card) is { } cardNode)
            {
                ReloadOverlay?.Invoke(cardNode);
            }
        }

        return new CmdResult(true, $"手牌 {cards.Count} 张已设为 {chord}。");
    }

    /// <summary>解析和弦参数：字母组合、位掩码整数或 <c>none</c>。</summary>
    private static bool TryParseChord(string text, out PerformChord chord)
    {
        chord = PerformChord.None;
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed.Equals(NoneKeyword, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (int.TryParse(trimmed, out var mask))
        {
            if (mask < 0 || mask > (int)PerformChord.All)
            {
                return false;
            }

            chord = (PerformChord)mask;
            return true;
        }

        foreach (var letter in trimmed.ToUpperInvariant())
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
