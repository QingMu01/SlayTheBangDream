using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils;
using BangDreamLib.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2RitsuLib.Scaffolding.Characters;

namespace BangDreamLib.Scripts.Character;

public abstract class BangDreamGroup(BangDreamBand band)
    : ModCharacterTemplate<DeprecatedCardPool, DeprecatedRelicPool, DeprecatedPotionPool>,
        IAggregationGroup
{
    public BangDreamBand Band => band;

    public override bool IsPlayable => false;

    public bool HasEffectiveMember { get; set; }

    public sealed override CharacterGender Gender => CharacterGender.Neutral;

    public sealed override Color NameColor => StsColors.gold;

    public sealed override int StartingHp => 1;

    public sealed override int StartingGold => 1;

    public override bool AllowInVanillaRandomCharacterSelect => false;

    public sealed override bool HideInCardLibraryCompendium => true;

    public sealed override string? CustomCharacterSelectBgPath => PreloadKey.CharacterSelector.GetPath();

    protected sealed override string CharacterSelectIconPath => Band.GetSelectIcon();

    protected sealed override string CharacterSelectLockedIconPath => Band.GetSelectIconLocked();

    public sealed override float AttackAnimDelay => 0.0f;

    public sealed override float CastAnimDelay => 0.0f;

    public sealed override List<string> GetArchitectAttackVfx() => [];

    public sealed override Color EnergyLabelOutlineColor => Colors.Magenta;

    public sealed override Color DialogueColor => Colors.Magenta;

    public sealed override Color MapDrawingColor => Colors.Magenta;

    public sealed override Color RemoteTargetingLineColor => Colors.Magenta;

    public sealed override Color RemoteTargetingLineOutline => Colors.Magenta;
}

public class PoppinParty() : BangDreamGroup(BangDreamBand.PoppinParty);

public class Afterglow() : BangDreamGroup(BangDreamBand.Afterglow);

public class PastelPalettes() : BangDreamGroup(BangDreamBand.PastelPalettes);

public class Roselia() : BangDreamGroup(BangDreamBand.Roselia);

public class HelloHappyWorld() : BangDreamGroup(BangDreamBand.HelloHappyWorld);

public class Morfonica() : BangDreamGroup(BangDreamBand.Morfonica);

public class RaiseASuilen() : BangDreamGroup(BangDreamBand.RaiseASuilen);

public class MyGo() : BangDreamGroup(BangDreamBand.MyGo);

public class AveMujica() : BangDreamGroup(BangDreamBand.AveMujica);

public class YumemitaMewType() : BangDreamGroup(BangDreamBand.YumemitaMewType);

public class Millsage() : BangDreamGroup(BangDreamBand.Millsage);

public class IkaDumbRock() : BangDreamGroup(BangDreamBand.IkaDumbRock);

public class Crychic() : BangDreamGroup(BangDreamBand.Crychic);