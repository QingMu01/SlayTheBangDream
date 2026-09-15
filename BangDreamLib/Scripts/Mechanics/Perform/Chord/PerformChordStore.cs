using BangDreamLib.Scripts.Enums;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Utils;

namespace BangDreamLib.Scripts.Mechanics.Perform.Chord;

/// <summary>
/// 卡牌和弦的持久化存储：和弦随卡牌经原版 <c>SavedProperties</c> 存档序列化。
/// 本轮仅提供读写入口，尚未接入"打开卡牌奖励时随机赋予和弦"的赋予逻辑。
/// </summary>
public static class PerformChordStore
{
    private static readonly SavedAttachedState<CardModel, PerformChord> Chords =
        new("PerformChord", () => PerformChord.None);

    /// <summary>
    /// 强制初始化存储字段。必须在内容注册阶段调用，以确保存档属性名在原版
    /// <c>SavedProperties</c> 缓存定稿前完成登记，否则首次于战斗中访问将抛出。
    /// </summary>
    public static void EnsureRegistered()
    {
        _ = Chords;
    }

    /// <summary>
    /// 取卡牌的和弦；未赋予时返回 <see cref="PerformChord.None" />。
    /// </summary>
    public static PerformChord GetChord(CardModel card)
    {
        return Chords.GetOrCreate(card);
    }

    /// <summary>
    /// 设置卡牌的和弦。预留给后续的奖励赋予系统调用。
    /// </summary>
    public static void SetChord(CardModel card, PerformChord chord)
    {
        Chords.Set(card, chord);
    }
}
