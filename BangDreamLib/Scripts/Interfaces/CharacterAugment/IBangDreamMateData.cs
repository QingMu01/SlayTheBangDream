namespace BangDreamLib.Scripts.Interfaces.CharacterAugment;

/// <summary>
/// BangDream 角色元数据
/// </summary>
public interface IBangDreamMateData
{
    /// <summary>
    /// BangDream角色名罗马字
    /// </summary>
    string MemberNameRoman { get; }

    /// <summary>
    /// BangDream角色在乐队中负责的位置
    /// </summary>
    string MemberClass { get; }

    /// <summary>
    /// 角色海报路径
    /// </summary>
    string? SelectPoster { get; }

    /// <summary>
    /// 角色代表图标
    /// </summary>
    string? SelectLogo { get; }
}