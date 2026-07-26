namespace BangDreamLib.Scripts.Interfaces.CharacterAugment;

public interface IPerformableCharacter : IExtraDeckSupportCharacter
{
    int GetDefaultCapacity { get; }
}