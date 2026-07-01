using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils.Enums;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Characters;

namespace BangDreamLib.Scripts.Character;

public abstract class BandMemberModel<TCardPoolModel, TRelicPoolModel, TPotionPoolModel>(Color mainColor)
    : ModCharacterTemplate<TCardPoolModel, TRelicPoolModel, TPotionPoolModel>,
        IPerformableCharacter, ISkinSupportCharacter, IGroupableCharacter, IBangDreamMateData
    where TCardPoolModel : CardPoolModel
    where TRelicPoolModel : RelicPoolModel
    where TPotionPoolModel : PotionPoolModel
{
    public override float AttackAnimDelay => 0.15f;
    public override float CastAnimDelay => 0.25f;

    public sealed override CharacterGender Gender => CharacterGender.Feminine;

    public sealed override Color NameColor => mainColor;
    public sealed override Color EnergyLabelOutlineColor => mainColor;
    public sealed override Color MapDrawingColor => mainColor;

    public abstract CharacterGroup Group { get; }

    public virtual bool IsHidden => false;
    public virtual bool AllowSelect => true;

    public abstract string MemberNameRoman { get; }
    public abstract string MemberClass { get; }
    public abstract string? SelectPoster { get; }

    public abstract override string CustomTrailPath { get; }
    public abstract override string CustomIconPath { get; }
    public abstract override string CustomIconTexturePath { get; }
    public abstract override string CustomIconOutlineTexturePath { get; }

    public override bool RequiresEpochAndTimeline => false;

    public abstract List<string> CharacterSkinList { get; }

    public abstract int GetDefaultCapacity { get; }

    public override List<string> GetArchitectAttackVfx()
    {
        return
        [
            "vfx/vfx_attack_blunt",
            "vfx/vfx_heavy_blunt",
            "vfx/vfx_attack_slash",
            "vfx/vfx_bloody_impact",
            "vfx/vfx_rock_shatter"
        ];
    }
}