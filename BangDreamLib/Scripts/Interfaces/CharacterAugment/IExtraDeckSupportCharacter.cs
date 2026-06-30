using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Interfaces.CharacterAugment;

public interface IExtraDeckSupportCharacter
{
    /// <summary>
    /// 是否总是显示额外卡组
    /// </summary>
    bool ShouldAlwaysShowExtraDeck { get; }
    
    /// <summary>
    /// 是否总是显示额外抽牌堆
    /// </summary>
    bool ShouldAlwaysShowExtraPile { get; }

    /// <summary>
    /// 额外卡池
    /// </summary>
    CardPoolModel ExtraCardPool { get; }
}