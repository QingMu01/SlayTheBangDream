using BangDreamLib.Scripts.Utils.Infos;
using Godot;
using STS2RitsuLib.Scaffolding.Content;

namespace ItsCrychic.Scripts.Character.RelicPools;

public class SakikoRelicPool : TypeListRelicPoolModel
{
    public override string EnergyColorName => BangDreamMember.Sakiko.GetMemberName();
    public override Color LabOutlineColor => BangDreamMember.Sakiko.GetMemberColor();
}