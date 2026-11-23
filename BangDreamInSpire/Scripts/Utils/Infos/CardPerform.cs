using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamInSpire.Scripts.Utils.Infos;

public sealed class CardPerform
{
    public required CardModel Card { get; init; }

    public required Player Player { get; init; }

    public required int SlotIndex { get; init; }

    /// <summary>False 表示由外部代码主动发起演奏。</summary>
    public required bool IsAutoPerform { get; init; }

    public required bool IsInstant { get; init; }

    public required bool IsSubsideTriggered { get; init; }
}