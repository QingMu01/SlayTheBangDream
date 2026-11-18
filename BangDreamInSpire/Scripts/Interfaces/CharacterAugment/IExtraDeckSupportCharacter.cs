using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Interfaces.CharacterAugment;

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

    /// <summary>
    /// 商人房间使用的角色按钮场景（内容模组提供）。为空表示该角色不提供自定义商人形象；
    /// 核心会回落到其它已注册角色的按钮场景，避免把具体角色资源写进库代码。
    /// </summary>
    string MerchantCharacterButtonScenePath => "";
}