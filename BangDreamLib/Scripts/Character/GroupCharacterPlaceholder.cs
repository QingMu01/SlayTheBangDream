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

    public override CharacterGender Gender => CharacterGender.Neutral;

    protected override Type? UnlocksAfterRunAsType => null;

    public override Color NameColor => StsColors.gold;

    public override int StartingHp => 1;

    public override int StartingGold => 1;

    public override bool HideFromVanillaCharacterSelect => true;

    public override bool AllowInVanillaRandomCharacterSelect => false;

    public override bool HideInCardLibraryCompendium => true;

    public override string? CustomCharacterSelectBgPath => PreloadKey.CharacterSelector.GetPath();

    protected override string CharacterSelectIconPath =>
        ImageHelper.GetImagePath("packed/character_select/char_select_random.png");

    protected override string CharacterSelectLockedIconPath =>
        ImageHelper.GetImagePath("packed/character_select/char_select_random_locked.png");

    public override float AttackAnimDelay => 0.0f;

    public override float CastAnimDelay => 0.0f;

    public override List<string> GetArchitectAttackVfx() => [];

    public override Color EnergyLabelOutlineColor => Colors.Magenta;

    public override Color DialogueColor => Colors.Magenta;

    public override Color MapDrawingColor => Colors.Magenta;

    public override Color RemoteTargetingLineColor => Colors.Magenta;

    public override Color RemoteTargetingLineOutline => Colors.Magenta;
}