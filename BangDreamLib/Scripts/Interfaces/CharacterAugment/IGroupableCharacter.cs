using BangDreamLib.Scripts.Enums;

namespace BangDreamLib.Scripts.Interfaces.CharacterAugment;

/// <summary>
/// 角色分组，实现该接口的角色将根据Group并入同一个按钮
/// 通过子菜单确定最终选择的角色
/// </summary>
public interface IGroupableCharacter
{
    /// <summary>
    /// 角色分组，同一分组的角色，在角色选择页面共享同一个选择按钮
    /// </summary>
    CharacterGroup Group { get; }

    /// <summary>
    /// 是否在角色选择页面中隐藏
    /// </summary>
    bool IsHidden { get; }

    /// <summary>
    /// 是否允许选择该角色
    /// </summary>
    bool AllowSelect { get; }
}