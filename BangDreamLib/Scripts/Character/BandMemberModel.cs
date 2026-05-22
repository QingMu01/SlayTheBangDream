using BangDreamLib.Scripts.Features;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Characters;

namespace BangDreamLib.Scripts.Character;

public abstract class BandMemberModel<TCardPoolModel, TRelicPoolModel, TPotionPoolModel>(Color mainColor)
    : ModCharacterTemplate<TCardPoolModel, TRelicPoolModel, TPotionPoolModel>,
        IPerformanceCharacter, ISkinSupportCharacter, IAggregationCharacter
    where TCardPoolModel : CardPoolModel
    where TRelicPoolModel : RelicPoolModel
    where TPotionPoolModel : PotionPoolModel
{
    public override CharacterGender Gender => CharacterGender.Feminine;

    public override float AttackAnimDelay => 0.15f;
    public override float CastAnimDelay => 0.25f;

    public sealed override Color NameColor => mainColor;
    public sealed override Color EnergyLabelOutlineColor => mainColor;
    public sealed override Color MapDrawingColor => mainColor;
    public virtual bool IsHidden => false;
    public virtual bool AllowSelect => true;
    public override bool HideFromVanillaCharacterSelect => true;
    public override bool AllowInVanillaRandomCharacterSelect => true;
    public override bool RequiresEpochAndTimeline => false;
    public abstract BangDreamBand Band { get; }
    public abstract string MemberNameRoman { get; }
    public abstract string MemberClass { get; }
    public abstract string? SelectIcon { get; }
    public abstract string? SelectPoster { get; }
    public abstract List<string> CharacterSkinList { get; }

    public abstract int GetDefaultCapacity { get; }

    public override string? CustomEnergyCounterPath
    {
        get
        {
            var (index, skinInfos) = SkinManager.GetCharacterSkins(GetType());
            string? path = null;
            if (index >= 0)
            {
                path = skinInfos[index].SkinTemplate.LocalVisual.EnergyCounterScene;
            }

            if (path == null)
            {
                path = CharacterAssetProfiles.Ironclad().Scenes?.EnergyCounterPath;
                BangDreamLibCore.Logger.Warn("Asset(EnergyCounter) is empty! Use Ironclad's EnergyCounter.");
            }

            return path;
        }
    }

    public override string? CustomIconPath
    {
        get
        {
            var (index, skinInfos) = SkinManager.GetCharacterSkins(GetType());
            string? path = null;
            if (index >= 0)
            {
                path = skinInfos[index].SkinTemplate.LocalVisual.IconScene;
            }

            if (path == null)
            {
                path = CharacterAssetProfiles.Ironclad().Ui?.IconPath;
                BangDreamLibCore.Logger.Warn("Asset(Icon) is empty! Use Ironclad's Icon.");
            }

            return path;
        }
    }

    public override string? CustomIconTexturePath
    {
        get
        {
            var (index, skinInfos) = SkinManager.GetCharacterSkins(GetType());
            string? path = null;
            if (index >= 0)
            {
                path = skinInfos[index].SkinTemplate.LocalVisual.IconTexture;
            }

            if (path == null)
            {
                path = CharacterAssetProfiles.Ironclad().Ui?.IconTexturePath;
                BangDreamLibCore.Logger.Warn("Asset(IconTexture) is empty! Use Ironclad's IconTexture.");
            }

            return path;
        }
    }

    public override string? CustomIconOutlineTexturePath
    {
        get
        {
            var (index, skinInfos) = SkinManager.GetCharacterSkins(GetType());
            string? path = null;
            if (index >= 0)
            {
                path = skinInfos[index].SkinTemplate.LocalVisual.IconTextureOutline;
            }

            if (path == null)
            {
                path = CharacterAssetProfiles.Ironclad().Ui?.IconOutlineTexturePath;
                BangDreamLibCore.Logger.Warn("Asset(IconOutline) is empty! Use Ironclad's IconOutline.");
            }

            return path;
        }
    }

    public override string? CustomMapMarkerPath
    {
        get
        {
            var (index, skinInfos) = SkinManager.GetCharacterSkins(GetType());
            string? path = null;
            if (index >= 0)
            {
                path = skinInfos[index].SkinTemplate.LocalVisual.MapMarker;
            }

            if (path == null)
            {
                path = CharacterAssetProfiles.Ironclad().Ui?.MapMarkerPath;
                BangDreamLibCore.Logger.Warn("Asset(MapMarker) is empty! Use Ironclad's MapMarker.");
            }

            return path;
        }
    }

    public override string? CustomTrailPath
    {
        get
        {
            var (index, skinInfos) = SkinManager.GetCharacterSkins(GetType());
            string? path = null;
            if (index >= 0)
                path = skinInfos[index].SkinTemplate.LocalVfx.CardTrail;
            if (path == null)
            {
                path = CharacterAssetProfiles.Ironclad().Vfx?.TrailPath;
                BangDreamLibCore.Logger.Warn("Asset(CardTrailVfx) is empty! Use Ironclad's CardTrailVfx.");
            }

            return path;
        }
    }

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