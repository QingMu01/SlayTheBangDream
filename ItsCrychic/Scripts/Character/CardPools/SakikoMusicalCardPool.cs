using Godot;
using ItsCrychic.Scripts.Utils;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace ItsCrychic.Scripts.Character.CardPools;

public class SakikoMusicalCardPool : TypeListCardPoolModel
{
    public override string Title => CrychicMemberEnum.Sakiko.GetMemberName() + "MusicPool";
    public override string EnergyColorName => CrychicMemberEnum.Sakiko.GetMemberName();
    public override Color DeckEntryCardColor => new("406090");
    public override Color EnergyOutlineColor => new("1D375C");

    public override Material PoolFrameMaterial =>
        MaterialUtils.CreateReplaceHueShaderMaterial(
            CrychicMemberEnum.Sakiko.GetMemberColor().R,
            CrychicMemberEnum.Sakiko.GetMemberColor().G,
            CrychicMemberEnum.Sakiko.GetMemberColor().B,
            1.1f
        );

    public override string BigEnergyIconPath => "res://ItsCrychic/images/charui/sakiko/sakiko_big_energy.png";
    public override string TextEnergyIconPath => "res://ItsCrychic/images/charui/sakiko/sakiko_small_energy.png";

    public override bool IsColorless => false;
}