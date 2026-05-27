using BangDreamLib.Scripts.Utils.Infos;
using STS2RitsuLib.Scaffolding.Content;

namespace ItsCrychic.Scripts.Character.PotionPools;

public class SakikoPotionPool : TypeListPotionPoolModel
{
    public override string EnergyColorName => BangDreamMember.Sakiko.GetMemberName();

    public override string BigEnergyIconPath => "res://ItsCrychic/images/charui/sakiko/sakiko_big_energy.png";
    public override string TextEnergyIconPath => "res://ItsCrychic/images/charui/sakiko/sakiko_small_energy.png";
}