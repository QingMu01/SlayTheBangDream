using Godot;
using ItsCrychic.Scripts.Utils;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace ItsCrychic.Scripts.Character.CardPools;

public class MutsumiMusicalCardPool : TypeListCardPoolModel
{
    public override string Title => CrychicMemberEnum.Mutsumi.GetMemberName() + "MusicPool";
    public override string EnergyColorName => CrychicMemberEnum.Mutsumi.GetMemberName();
    public override Color DeckEntryCardColor => new("406090");
    public override Color EnergyOutlineColor => new("1D375C");

    public override Material PoolFrameMaterial =>
        MaterialUtils.CreateReplaceHueShaderMaterial(
            CrychicMemberEnum.Mutsumi.GetMemberColor().R,
            CrychicMemberEnum.Mutsumi.GetMemberColor().G,
            CrychicMemberEnum.Mutsumi.GetMemberColor().B
            );

    public override string BigEnergyIconPath => "res://ItsCrychic/images/charui/sakiko/sakiko_big_energy.png";
    public override string TextEnergyIconPath => "res://ItsCrychic/images/charui/sakiko/sakiko_small_energy.png";

    public override bool IsColorless => false;
}