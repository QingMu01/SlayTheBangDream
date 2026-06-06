using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Models;
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
            _extraCards = [];
            foreach (var cardModel in BangDreamTools.GetPile(BangDreamConst.ExtraDeck, Owner).Cards)
            {
                _extraCards.Add(cardModel.ToSerializable());
            }

            return _extraCards;
        }
        set => _extraCards = value;
    }

    public List<CardModel> GetSavedCards()
    {
        return _extraCards?.Select(CardModel.FromSerializable).ToList() ?? [];
    }
}