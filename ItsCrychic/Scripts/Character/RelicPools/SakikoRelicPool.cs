using Godot;
using ItsCrychic.Scripts.Utils;
using STS2RitsuLib.Scaffolding.Content;

namespace ItsCrychic.Scripts.Character.RelicPools;

public class SakikoRelicPool : TypeListRelicPoolModel
{
    public override string EnergyColorName => CrychicMemberEnum.Sakiko.GetMemberName();
    public override Color LabOutlineColor => CrychicMemberEnum.Sakiko.GetMemberColor();
}