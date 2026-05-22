using BangDreamLib.Scripts.Utils.Infos;
using Godot;
using STS2RitsuLib.Scaffolding.Content;

namespace ItsCrychic.Scripts.Character.RelicPools;

public class MutsumiRelicPool : TypeListRelicPoolModel
{
    public override string EnergyColorName => BangDreamMember.Mutsumi.GetMemberName();
    public override Color LabOutlineColor => BangDreamMember.Mutsumi.GetMemberColor();
}