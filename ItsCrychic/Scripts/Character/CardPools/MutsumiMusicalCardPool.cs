using BangDreamLib.Scripts.Utils.Infos;
using Godot;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace ItsCrychic.Scripts.Character.CardPools;

public class MutsumiMusicalCardPool : TypeListCardPoolModel
{
    public override string Title => BangDreamMember.Mutsumi.GetMemberName() + "MusicPool";
    public override string EnergyColorName => BangDreamMember.Mutsumi.GetMemberName();
    public override Color DeckEntryCardColor => new("406090");
    public override Color EnergyOutlineColor => new("1D375C");

    public override Material PoolFrameMaterial =>
        MaterialUtils.CreateHsvShaderMaterial(
            BangDreamMember.Mutsumi.GetMemberColor().H,
            BangDreamMember.Mutsumi.GetMemberColor().S,
            1f);

    public override string BigEnergyIconPath => "res://ItsCrychic/images/charui/sakiko/sakiko_big_energy.png";
    public override string TextEnergyIconPath => "res://ItsCrychic/images/charui/sakiko/sakiko_small_energy.png";

    public override bool IsColorless => false;
}