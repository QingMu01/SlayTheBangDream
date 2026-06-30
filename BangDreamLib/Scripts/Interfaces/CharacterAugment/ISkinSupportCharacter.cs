using STS2RitsuLib.Scaffolding.Characters;

namespace BangDreamLib.Scripts.Interfaces.CharacterAugment;

public interface ISkinSupportCharacter
{
    List<string> CharacterSkinList { get; }

    CharacterAssetProfile PlaceholderCharacterAssetProfile => CharacterAssetProfiles.Defect();
}