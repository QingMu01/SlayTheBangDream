namespace BangDreamInSpire.Scripts.Mechanics.Break;

/// <summary>
/// 破绽 / 弱点击破的规则常量。数值集中在此处，卡牌文案与实现都以此为准。
/// </summary>
public static class BreakConst
{
    /// <summary>单个单位可累计的破绽层数上限；达到该层数时进入[gold]弱点击破[/gold]。</summary>
    public const int MaxStacks = 4;

    /// <summary>[gold]弱点击破[/gold]持续的回合数（按目标一侧的回合结束结算）。</summary>
    public const int BreakDurationTurns = 1;
}
