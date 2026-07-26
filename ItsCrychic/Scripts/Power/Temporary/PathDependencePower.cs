using ItsCrychic.Scripts.Cards.Saki.Skill;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ItsCrychic.Scripts.Power.Temporary;

public class PathDependencePower : TemporaryDexterityPower
{
    public override AbstractModel OriginModel => ModelDb.Card<PathDependence>();
}
