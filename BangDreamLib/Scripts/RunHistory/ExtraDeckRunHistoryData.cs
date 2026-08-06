using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BangDreamLib.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Runs;
using GameRunHistory = MegaCrit.Sts2.Core.Runs.RunHistory;

namespace BangDreamLib.Scripts.RunHistory;

internal sealed class ExtraDeckRunHistoryData
{
    public const int CurrentVersion = 1;

    public Dictionary<ulong, List<SerializableCard>> ExtraDecks { get; } = [];

    public bool IsEmpty => ExtraDecks.Count == 0 || ExtraDecks.Values.All(cards => cards.Count == 0);
}

internal sealed record ExtraDeckRunHistoryLoadCandidate(long StartTime, ExtraDeckRunHistoryData? Data);

internal sealed class ExtraDeckRunHistoryLoadCapture
{
    public List<ExtraDeckRunHistoryLoadCandidate> Candidates { get; } = [];
}

internal static class ExtraDeckRunHistoryDataManager
{
    private const string RootPropertyName = "_bang_dream_lib";
    private const string VersionPropertyName = "version";
    private const string ExtraDecksPropertyName = "extra_decks";

    private static readonly ConditionalWeakTable<GameRunHistory, ExtraDeckRunHistoryData> HistoryData = new();
    private static readonly AsyncLocal<PendingSnapshot?> PendingSnapshotState = new();
    private static readonly JsonSerializerOptions NodeWriteOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public static IDisposable BeginSerializableRunCapture(SerializableRun run)
    {
        var previous = PendingSnapshotState.Value;
        ExtraDeckRunHistoryData? data = null;

        if (RunManager.Instance.DebugOnlyGetState() == null)
        {
            try
            {
                var restoredState = RunState.FromSerializable(run);
                data = CreateSnapshot(restoredState);
            }
            catch (Exception ex)
            {
                BangDreamLibCore.Logger.Warn(
                    $"Failed to restore extra decks from serialized run for history: {ex}");
            }
        }

        PendingSnapshotState.Value = new PendingSnapshot(run.StartTime, data);
        return new PendingSnapshotScope(previous);
    }

    public static void AttachSnapshotForSave(GameRunHistory history)
    {
        ExtraDeckRunHistoryData? data = null;
        var runState = RunManager.Instance.DebugOnlyGetState();

        if (runState != null)
        {
            try
            {
                data = CreateSnapshot(runState);
            }
            catch (Exception ex)
            {
                BangDreamLibCore.Logger.Warn($"Failed to snapshot extra decks for run history: {ex}");
            }
        }
        else if (PendingSnapshotState.Value is { } pending && pending.StartTime == history.StartTime)
        {
            data = pending.Data;
        }

        if (data == null)
        {
            BangDreamLibCore.Logger.Warn(
                $"No run state was available while saving run history {history.StartTime}; extra decks were skipped.");
            return;
        }

        SetData(history, data);
    }

    public static string SerializeHistory(GameRunHistory history)
    {
        var json = JsonSerializationUtility.ToJson(history);
        if (!HistoryData.TryGetValue(history, out var data) || data.IsEmpty)
            return json;

        try
        {
            if (JsonNode.Parse(json) is not JsonObject root)
                return json;

            root[RootPropertyName] = CreateExtensionNode(data);
            return root.ToJsonString(NodeWriteOptions);
        }
        catch (Exception ex)
        {
            BangDreamLibCore.Logger.Warn($"Failed to inject extra decks into run history JSON: {ex}");
            return json;
        }
    }

    public static ExtraDeckRunHistoryLoadCapture CaptureLoad(
        string fileName,
        ISaveStore saveStore,
        IProfileIdProvider profileIdProvider)
    {
        var capture = new ExtraDeckRunHistoryLoadCapture();
        try
        {
            var path = Path.Combine(RunHistorySaveManager.GetHistoryPath(profileIdProvider.CurrentProfileId), fileName);
            CaptureCandidate(path, saveStore, capture);
            CaptureCandidate(path + ".backup", saveStore, capture);
        }
        catch (Exception ex)
        {
            BangDreamLibCore.Logger.Warn(
                $"Failed to prepare extra deck data while loading run history {fileName}: {ex}");
        }

        return capture;
    }

    public static void AttachLoadedData(
        ExtraDeckRunHistoryLoadCapture? capture,
        ReadSaveResult<GameRunHistory> result)
    {
        if (capture == null || !result.Success || result.SaveData == null)
            return;

        var candidate = capture.Candidates.FirstOrDefault(item => item.StartTime == result.SaveData.StartTime);
        if (candidate?.Data != null)
            SetData(result.SaveData, candidate.Data);
    }

    public static IReadOnlyList<SerializableCard> GetExtraDeck(GameRunHistory history, ulong playerId)
    {
        if (HistoryData.TryGetValue(history, out var data) &&
            data.ExtraDecks.TryGetValue(playerId, out var cards))
        {
            return cards;
        }

        return [];
    }

    private static ExtraDeckRunHistoryData CreateSnapshot(RunState runState)
    {
        var data = new ExtraDeckRunHistoryData();
        foreach (var player in runState.Players)
        {
            data.ExtraDecks[player.NetId] = BangDreamConst.ExtraDeck.GetPile(player).Cards
                .Select(card => card.ToSerializable())
                .ToList();
        }

        return data;
    }

    private static JsonObject CreateExtensionNode(ExtraDeckRunHistoryData data)
    {
        var extraDecks = new JsonObject();
        foreach (var (playerId, cards) in data.ExtraDecks.OrderBy(pair => pair.Key))
        {
            var serializedCards = new JsonArray();
            foreach (var card in cards)
            {
                serializedCards.Add(JsonSerializer.SerializeToNode(
                    card,
                    JsonSerializationUtility.GetTypeInfo<SerializableCard>()));
            }

            extraDecks[playerId.ToString(CultureInfo.InvariantCulture)] = serializedCards;
        }

        return new JsonObject
        {
            [VersionPropertyName] = ExtraDeckRunHistoryData.CurrentVersion,
            [ExtraDecksPropertyName] = extraDecks
        };
    }

    private static void CaptureCandidate(
        string path,
        ISaveStore saveStore,
        ExtraDeckRunHistoryLoadCapture capture)
    {
        if (!saveStore.FileExists(path))
            return;

        try
        {
            var content = saveStore.ReadFile(path);
            if (string.IsNullOrWhiteSpace(content) ||
                JsonNode.Parse(content) is not JsonObject root ||
                !TryGetStartTime(root, out var startTime))
            {
                return;
            }

            capture.Candidates.Add(new(startTime, ParseExtension(root, path)));
        }
        catch (JsonException)
        {
            // 原版迁移管理器会处理损坏的历史文件及其备份，这里不重复记录整份文件的解析错误。
        }
        catch (Exception ex)
        {
            BangDreamLibCore.Logger.Warn($"Failed to read extra deck run history data from {path}: {ex}");
        }
    }

    private static ExtraDeckRunHistoryData? ParseExtension(JsonObject root, string path)
    {
        if (!root.TryGetPropertyValue(RootPropertyName, out var extensionNode))
            return null;

        if (extensionNode is not JsonObject extension)
        {
            WarnInvalidExtension(path, "root node is not an object");
            return null;
        }

        if (!TryGetInt(extension[VersionPropertyName], out var version) ||
            version != ExtraDeckRunHistoryData.CurrentVersion)
        {
            WarnInvalidExtension(path, $"unsupported or missing version '{extension[VersionPropertyName]}'");
            return null;
        }

        if (extension[ExtraDecksPropertyName] is not JsonObject extraDecks)
        {
            WarnInvalidExtension(path, "extra_decks is not an object");
            return null;
        }

        var data = new ExtraDeckRunHistoryData();
        foreach (var (playerIdText, cardsNode) in extraDecks)
        {
            if (!ulong.TryParse(playerIdText, NumberStyles.None, CultureInfo.InvariantCulture, out var playerId))
            {
                WarnInvalidExtension(path, $"invalid player NetId '{playerIdText}'");
                continue;
            }

            if (cardsNode is not JsonArray cardsArray)
            {
                WarnInvalidExtension(path, $"extra deck for player {playerId} is not an array");
                continue;
            }

            var cards = new List<SerializableCard>();
            foreach (var cardNode in cardsArray)
            {
                if (cardNode == null)
                {
                    WarnInvalidExtension(path, $"extra deck for player {playerId} contains a null card");
                    continue;
                }

                try
                {
                    var card = JsonSerializer.Deserialize(
                        cardNode.ToJsonString(),
                        JsonSerializationUtility.GetTypeInfo<SerializableCard>());
                    if (card?.Id != null)
                        cards.Add(card);
                    else
                        WarnInvalidExtension(path, $"extra deck for player {playerId} contains a card without an id");
                }
                catch (Exception ex)
                {
                    WarnInvalidExtension(path, $"failed to deserialize a card for player {playerId}: {ex.Message}");
                }
            }

            data.ExtraDecks[playerId] = cards;
        }

        return data;
    }

    private static bool TryGetStartTime(JsonObject root, out long startTime)
    {
        startTime = 0;
        return root["start_time"] is JsonValue value && value.TryGetValue(out startTime);
    }

    private static bool TryGetInt(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue jsonValue && jsonValue.TryGetValue(out value);
    }

    private static void SetData(GameRunHistory history, ExtraDeckRunHistoryData data)
    {
        HistoryData.Remove(history);
        HistoryData.Add(history, data);
    }

    private static void WarnInvalidExtension(string path, string reason)
    {
        BangDreamLibCore.Logger.Warn($"Ignored invalid extra deck run history data in {path}: {reason}.");
    }

    private sealed record PendingSnapshot(long StartTime, ExtraDeckRunHistoryData? Data);

    private sealed class PendingSnapshotScope(PendingSnapshot? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            PendingSnapshotState.Value = previous;
        }
    }
}
