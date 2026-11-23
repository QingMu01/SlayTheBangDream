using BangDreamInSpire.Scripts.Enums;

namespace BangDreamInSpire.Scripts.Interfaces.CharacterAugment;

/// <summary>同组角色共享选择按钮，并通过子菜单选择具体角色。</summary>
public interface IGroupableCharacter
{
    CharacterGroup Group { get; }

    bool IsHidden { get; }

    bool AllowSelect { get; }
}