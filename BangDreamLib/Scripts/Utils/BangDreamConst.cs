using BangDreamLib.Scripts.Multiplayer.RunData;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Rewards;
using STS2RitsuLib.RunData;

namespace BangDreamLib.Scripts.Utils;

public static class BangDreamConst
{
    public const string ModId = "BangDreamLib";
    public const string RunDataKeySkin = "RunSkin";

    private static PlayerRunSavedData<PlayerSkinData>? _playerSkin;

    public static PlayerRunSavedData<PlayerSkinData> PlayerSkin
    {
        get => _playerSkin ?? throw new InvalidOperationException("PlayerRunSavedData.PlayerSkin is not initialized.");
        internal set => Init(ref _playerSkin, value, nameof(PlayerSkin));
    }

    private static PileType? _extraDeck;

    public static PileType ExtraDeck
    {
        get => _extraDeck ?? throw new InvalidOperationException("PileType.ExtraDeck is not initialized.");
        internal set => Init(ref _extraDeck, value, nameof(ExtraDeck));
    }

    private static PileType? _extraDraw;

    public static PileType ExtraDraw
    {
        get => _extraDraw ?? throw new InvalidOperationException("PileType.ExtraDraw is not initialized.");
        internal set => Init(ref _extraDraw, value, nameof(ExtraDraw));
    }

    private static PileType? _performanceTable;

    public static PileType PerformanceTable
    {
        get => _performanceTable ?? throw new InvalidOperationException("PileType.PerformanceTable is not initialized.");
        internal set => Init(ref _performanceTable, value, nameof(PerformanceTable));
    }

    private static CardKeyword? _music;

    public static CardKeyword Music
    {
        get => _music ?? throw new InvalidOperationException("CardKeyword.Music is not initialized.");
        internal set => Init(ref _music, value, nameof(Music));
    }

    private static CardKeyword? _musicNote;

    public static CardKeyword MusicNote
    {
        get => _musicNote ?? throw new InvalidOperationException("CardKeyword.MusicNote is not initialized.");
        internal set => Init(ref _musicNote, value, nameof(MusicNote));
    }

    private static CardKeyword? _performance;

    public static CardKeyword Performance
    {
        get => _performance ?? throw new InvalidOperationException("CardKeyword.Performance is not initialized.");
        internal set => Init(ref _performance, value, nameof(Performance));
    }

    private static CardKeyword? _instant;

    public static CardKeyword Instant
    {
        get => _instant ?? throw new InvalidOperationException("CardKeyword.Instant is not initialized.");
        internal set => Init(ref _instant, value, nameof(Instant));
    }

    private static CardKeyword? _performanceArea;

    public static CardKeyword PerformanceArea
    {
        get => _performanceArea ?? throw new InvalidOperationException("CardKeyword.PerformanceArea is not initialized.");
        internal set => Init(ref _performanceArea, value, nameof(PerformanceArea));
    }

    private static CardKeyword? _linger;

    public static CardKeyword Linger
    {
        get => _linger ?? throw new InvalidOperationException("CardKeyword.Linger is not initialized.");
        internal set => Init(ref _linger, value, nameof(Linger));
    }

    private static RewardType? _rewardMusic;

    public static RewardType RewardMusic
    {
        get => _rewardMusic ?? throw new InvalidOperationException("RewardType.RewardMusic is not initialized.");
        internal set => Init(ref _rewardMusic, value, nameof(RewardMusic));
    }

    private static CardTag? _symbolCard;

    public static CardTag SymbolCard
    {
        get => _symbolCard ?? throw new InvalidOperationException("CardTag.SymbolCard is not initialized.");
        internal set => Init(ref _symbolCard, value, nameof(SymbolCard));
    }

    private static void Init<T>(ref T? storage, T value, string propertyName) where T : class
    {
        if (storage is not null)
            throw new InvalidOperationException($"{propertyName} is already initialized!");
        storage = value;
    }

    private static void Init<T>(ref T? storage, T value, string propertyName) where T : struct
    {
        if (storage.HasValue)
            throw new InvalidOperationException($"{propertyName} is already initialized!");
        storage = value;
    }
}