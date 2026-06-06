using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Rewards;

namespace BangDreamLib.Scripts.Utils;

public static class BangDreamConst
{
    public const string ModId = "BangDreamLib";

    public const string SaveKeySkin = "SkinConfig";

    private static PileType? _extraDeck;

    public static PileType ExtraDeck
    {
        get => _extraDeck ?? throw new NullReferenceException("PileType.ExtraDeck is null.");
        set
        {
            if (_extraDeck == null)
            {
                _extraDeck = value;
            }
            else
            {
                throw new InvalidOperationException("PileType.ExtraDeck is already set.");
            }
        }
    }

    private static PileType? _extraDraw;

    public static PileType ExtraDraw
    {
        get => _extraDraw ?? throw new NullReferenceException("PileType.ExtraDraw is null.");
        set
        {
            if (_extraDraw == null)
            {
                _extraDraw = value;
            }
            else
            {
                throw new InvalidOperationException("PileType.ExtraDraw is already set.");
            }
        }
    }

    private static PileType? _performanceTable;

    public static PileType PerformanceTable
    {
        get => _performanceTable ?? throw new NullReferenceException("PileType.Performance is null.");
        set
        {
            if (_performanceTable == null)
            {
                _performanceTable = value;
            }
            else
            {
                throw new InvalidOperationException("PileType.Performance is already set.");
            }
        }
    }

    private static CardKeyword? _music;

    public static CardKeyword Music
    {
        get => _music ?? throw new NullReferenceException("CardKeyword.Music is null.");
        set
        {
            if (_music == null)
            {
                _music = value;
            }
            else
            {
                throw new InvalidOperationException("CardKeyword.Music is already set.");
            }
        }
    }

    private static CardKeyword? _musicNote;

    public static CardKeyword MusicNote
    {
        get => _musicNote ?? throw new NullReferenceException("CardKeyword.MusicNote is null.");
        set
        {
            if (_musicNote == null)
            {
                _musicNote = value;
            }
            else
            {
                throw new InvalidOperationException("CardKeyword.MusicNote is already set.");
            }
        }
    }

    private static CardKeyword? _performance;

    public static CardKeyword Performance
    {
        get => _performance ?? throw new NullReferenceException("CardKeyword.Performance is null.");
        set
        {
            if (_performance == null)
            {
                _performance = value;
            }
            else
            {
                throw new InvalidOperationException("CardKeyword.Performance is already set.");
            }
        }
    }

    private static CardKeyword? _performanceArea;

    public static CardKeyword PerformanceArea
    {
        get => _performanceArea ?? throw new NullReferenceException("CardKeyword.PerformanceArea is null.");
        set
        {
            if (_performanceArea == null)
            {
                _performanceArea = value;
            }
            else
            {
                throw new InvalidOperationException("CardKeyword.PerformanceArea is already set.");
            }
        }
    }

    private static CardKeyword? _linger;

    public static CardKeyword Linger
    {
        get => _linger ?? throw new NullReferenceException("CardKeyword.Linger is null.");
        set
        {
            if (_linger == null)
            {
                _linger = value;
            }
            else
            {
                throw new InvalidOperationException("CardKeyword.Linger is already set.");
            }
        }
    }

    private static RewardType? _rewardMusic;

    public static RewardType RewardMusic
    {
        get => _rewardMusic ?? throw new NullReferenceException("RewardType.RewardMusic is null.");
        set
        {
            if (_rewardMusic == null)
            {
                _rewardMusic = value;
            }
            else
            {
                throw new InvalidOperationException("RewardType.RewardMusic is already set.");
            }
        }
    }
}