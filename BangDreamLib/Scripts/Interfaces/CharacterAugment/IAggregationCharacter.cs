using BangDreamLib.Scripts.Utils.Infos;
using STS2RitsuLib.Scaffolding.Characters;

namespace BangDreamLib.Scripts.Interfaces.CharacterAugment;

public interface IAggregationCharacter : IModCharacterVanillaSelectionPolicy
{
    BangDreamBand Band { get; }

    string MemberNameRoman { get; }
    string MemberClass { get; }

    bool IsHidden { get; }
    bool AllowSelect { get; }

    string? SelectIcon { get; }
    string? SelectPoster { get; }
}