using Godot;
using ItsCrychic.Scripts.Utils;
using STS2RitsuLib.Scaffolding.Content;

namespace ItsCrychic.Scripts.Character.RelicPools;

public class MutsumiRelicPool : TypeListRelicPoolModel
{
    public override string EnergyColorName => CrychicMemberEnum.Mutsumi.GetMemberName();
    public override Color LabOutlineColor => CrychicMemberEnum.Mutsumi.GetMemberColor();
}