using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Interfaces.CharacterAugment;

public interface IExtraDeckSupportCharacter
{
    /// <summary>
    /// 是否总是显示额外卡组和牌组
    /// </summary>
    bool ShouldAlwaysShowExtraDeckAndPile { get; }

    /// <summary>
    /// 额外卡池
    /// </summary>
    CardPoolModel ExtraCardPool { get; }
}