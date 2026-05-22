using BangDreamLib.Scripts.Extensions;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace BangDreamLib.Scripts.Relics.GameRules;

public class ExtraDeckSaveHelper : HiddenRelic
{
    private List<SerializableCard>? _extraCards;

    [SavedProperty]
    public List<SerializableCard> ExtraCards
    {
        get
        {
            if (_extraCards != null)
            {
                return _extraCards;
            }

            _extraCards = [];
            foreach (var cardModel in BangDreamConst.PileExtraDeck.GetPile(Owner).Cards)
            {
                _extraCards.Add(cardModel.ToSerializable());
            }

            return _extraCards;
        }
        set => _extraCards = value;
    }
}