using ItsCrychic.Scripts.Cards.Saki.Music;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ItsCrychic.Scripts.Power.Temporary;

public class RhapsodyRequestPower : TemporaryDexterityPower
{
    public override AbstractModel OriginModel => ModelDb.Card<MasqueradeRhapsodyRequest>();
}