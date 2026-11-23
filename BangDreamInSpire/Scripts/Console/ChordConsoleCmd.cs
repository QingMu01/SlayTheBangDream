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

/// <summary>chord [letters|mask|none] 设置手牌和弦并刷新书签；仅单人或主机使用。</summary>
public class ChordConsoleCmd : AbstractConsoleCmd
{
    private const string NoneKeyword = "none";

    /// <summary>反射重建卡面覆盖层。</summary>
    private static readonly Action<NCard>? ReloadOverlay = AccessTools.DeclaredMethod(typeof(NCard), "ReloadOverlay")
        is { } method
        ? AccessTools.MethodDelegate<Action<NCard>>(method)
        : null;

    public override string CmdName => "chord";

    public override string Args => "<C|D|F|G 组合 / 位掩码 1/2/4/8 / none>";

    public override string Description => "dev: 给手牌设置演奏和弦，预览右边缘和弦书签";

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

    /// <summary>解析字母、位掩码或 none。</summary>
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
