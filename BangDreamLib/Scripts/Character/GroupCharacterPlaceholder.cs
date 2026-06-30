using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2RitsuLib.Scaffolding.Characters;

namespace BangDreamLib.Scripts.Character;

public sealed class GroupCharacterPlaceholder :
    ModCharacterTemplate<DeprecatedCardPool, DeprecatedRelicPool, DeprecatedPotionPool>
{
    public override bool IsPlayable => false;

    public sealed override CharacterGender Gender => CharacterGender.Neutral;

    protected override Type? UnlocksAfterRunAsType => null;

    public sealed override Color NameColor => StsColors.gold;

    public sealed override int StartingHp => 1;

    public sealed override int StartingGold => 1;

    public override bool HideFromVanillaCharacterSelect => true;

    public override bool AllowInVanillaRandomCharacterSelect => false;

    public sealed override bool HideInCardLibraryCompendium => true;

    public sealed override string? CustomCharacterSelectBgPath => PreloadKey.CharacterSelector.GetPath();

    protected sealed override string CharacterSelectIconPath =>
        ImageHelper.GetImagePath("packed/character_select/char_select_random.png");

    protected sealed override string CharacterSelectLockedIconPath =>
        ImageHelper.GetImagePath("packed/character_select/char_select_random_locked.png");

    public sealed override float AttackAnimDelay => 0.0f;

    public sealed override float CastAnimDelay => 0.0f;

    public sealed override List<string> GetArchitectAttackVfx() => [];

    public sealed override Color EnergyLabelOutlineColor => Colors.Magenta;

    public sealed override Color DialogueColor => Colors.Magenta;

    public sealed override Color MapDrawingColor => Colors.Magenta;

    public sealed override Color RemoteTargetingLineColor => Colors.Magenta;

    public sealed override Color RemoteTargetingLineOutline => Colors.Magenta;
}