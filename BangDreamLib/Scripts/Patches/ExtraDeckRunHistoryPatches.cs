using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using BangDreamLib.Scripts.RunHistory;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;
using STS2RitsuLib.Utils.HarmonyIl;
using GameRunHistory = MegaCrit.Sts2.Core.Runs.RunHistory;

namespace BangDreamLib.Scripts.Patches;

internal sealed class ExtraDeckRunHistoryPatches : IModPatches
{
    public static void AddTo(ModPatcher patcher)
    {
        patcher.RegisterPatch<CaptureSerializableRunExtraDeckPatch>();
        patcher.RegisterPatch<AttachExtraDeckSnapshotPatch>();
        patcher.RegisterPatch<SerializeExtraDeckRunHistoryPatch>();
        patcher.RegisterPatch<LoadExtraDeckRunHistoryPatch>();
        patcher.RegisterPatch<SetupExtraDeckRunHistoryViewPatch>();
        patcher.RegisterPatch<LoadExtraDeckRunHistoryViewPatch>();
    }
}

internal sealed class CaptureSerializableRunExtraDeckPatch : IPatchMethod
{
    public static string PatchId => "capture_serialized_extra_deck_for_run_history";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(RunHistoryUtilities), nameof(RunHistoryUtilities.CreateRunHistoryEntry),
                [typeof(SerializableRun), typeof(bool), typeof(bool), typeof(PlatformType)])
        ];
    }

    public static void Prefix(SerializableRun run, out IDisposable __state)
    {
        __state = ExtraDeckRunHistoryDataManager.BeginSerializableRunCapture(run);
    }

    public static void Finalizer(IDisposable? __state)
    {
        __state?.Dispose();
    }
}

internal sealed class AttachExtraDeckSnapshotPatch : IPatchMethod
{
    public static string PatchId => "attach_extra_deck_snapshot_to_run_history";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(SaveManager), nameof(SaveManager.SaveRunHistory), [typeof(GameRunHistory)])];
    }

    public static void Prefix(GameRunHistory history)
    {
        ExtraDeckRunHistoryDataManager.AttachSnapshotForSave(history);
    }
}

internal sealed class SerializeExtraDeckRunHistoryPatch : IPatchMethod
{
    private static readonly MethodInfo ToJsonDefinition = AccessTools.DeclaredMethod(
        typeof(JsonSerializationUtility),
        nameof(JsonSerializationUtility.ToJson));

    private static readonly MethodInfo SerializeHistoryMethod = AccessTools.DeclaredMethod(
        typeof(ExtraDeckRunHistoryDataManager),
        nameof(ExtraDeckRunHistoryDataManager.SerializeHistory));

    public static string PatchId => "inject_extra_deck_into_run_history_json";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(RunHistorySaveManager), nameof(RunHistorySaveManager.SaveHistory),
                [typeof(GameRunHistory)])
        ];
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var rewriter = HarmonyIlRewriter.From(instructions);
        const string operation = "Replace run history serialization with extra deck serialization";
        var report = rewriter.RedirectCalls(
            operation,
            called => IsRunHistoryToJson(called) ? SerializeHistoryMethod : null,
            code => code.Any(instruction => instruction.Calls(SerializeHistoryMethod)));
        report.RequireApplied();
        return rewriter.InstructionsChecked(operation);
    }

    private static bool IsRunHistoryToJson(MethodInfo method)
    {
        if (!method.IsGenericMethod || method.GetGenericMethodDefinition() != ToJsonDefinition)
            return false;

        var arguments = method.GetGenericArguments();
        return arguments.Length == 1 && arguments[0] == typeof(GameRunHistory);
    }
}

internal sealed class LoadExtraDeckRunHistoryPatch : IPatchMethod
{
    public static string PatchId => "load_extra_deck_from_run_history_json";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return
        [
            new ModPatchTarget(typeof(RunHistorySaveManager), nameof(RunHistorySaveManager.LoadHistory),
                [typeof(string)])
        ];
    }

    public static void Prefix(
        string fileName,
        ISaveStore ____saveStore,
        IProfileIdProvider ____profileIdProvider,
        out ExtraDeckRunHistoryLoadCapture __state)
    {
        __state = ExtraDeckRunHistoryDataManager.CaptureLoad(fileName, ____saveStore, ____profileIdProvider);
    }

    public static void Postfix(
        ReadSaveResult<GameRunHistory> __result,
        ExtraDeckRunHistoryLoadCapture? __state)
    {
        ExtraDeckRunHistoryDataManager.AttachLoadedData(__state, __result);
    }
}

internal sealed class SetupExtraDeckRunHistoryViewPatch : IPatchMethod
{
    private const int DuplicateGroupsAndScripts = 6;

    public static string PatchId => "setup_extra_deck_run_history_view";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NRunHistory), nameof(NRunHistory._Ready))];
    }

    public static void Postfix(
        NRunHistory __instance,
        NDeckHistory ____deckHistory,
        NMapPointHistory ____mapPointHistory)
    {
        try
        {
            // 不复制原牌组栏已连接的信号，额外卡组只建立自己的一组楼层高亮连接。
            // Godot DuplicateFlags.Groups | DuplicateFlags.Scripts；当前 GodotSharp 版本只暴露 int 参数。
            var duplicate = ____deckHistory.Duplicate(DuplicateGroupsAndScripts);
            if (duplicate is not NDeckHistory extraDeckHistory)
            {
                BangDreamLibCore.Logger.Warn("Failed to duplicate NDeckHistory for extra deck run history view.");
                duplicate.QueueFreeSafely();
                return;
            }

            extraDeckHistory.Name = "BangDreamExtraDeckHistory";
            extraDeckHistory.UniqueNameInOwner = false;
            extraDeckHistory.Visible = false;
            ReassignSubtreeOwner(extraDeckHistory, extraDeckHistory);

            var cardContainer = extraDeckHistory.FindChild("CardContainer", recursive: true, owned: false);
            if (cardContainer == null)
            {
                BangDreamLibCore.Logger.Warn(
                    "Duplicated NDeckHistory does not contain CardContainer; extra deck history view was skipped.");
                extraDeckHistory.QueueFreeSafely();
                return;
            }

            cardContainer.UniqueNameInOwner = true;
            cardContainer.Owner = extraDeckHistory;

            var parent = ____deckHistory.GetParent();
            parent.AddChildSafely(extraDeckHistory);
            parent.MoveChildSafely(extraDeckHistory, ____deckHistory.GetIndex() + 1);
            ____mapPointHistory.SetDeckHistory(extraDeckHistory);
            ExtraDeckRunHistoryViewState.Set(__instance, extraDeckHistory);
        }
        catch (Exception ex)
        {
            BangDreamLibCore.Logger.Warn($"Failed to set up extra deck run history view: {ex}");
        }
    }

    private static void ReassignSubtreeOwner(Node node, Node owner)
    {
        node.Owner = ReferenceEquals(node, owner) ? null : owner;
        foreach (var child in node.GetChildren())
            ReassignSubtreeOwner(child, owner);
    }
}

internal sealed class LoadExtraDeckRunHistoryViewPatch : IPatchMethod
{
    public static string PatchId => "load_extra_deck_run_history_view";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets()
    {
        return [new ModPatchTarget(typeof(NRunHistory), "SelectPlayer", [typeof(NRunHistoryPlayerIcon)])];
    }

    public static void Postfix(
        NRunHistory __instance,
        NRunHistoryPlayerIcon playerIcon,
        GameRunHistory ____history)
    {
        if (!ExtraDeckRunHistoryViewState.TryGet(__instance, out var extraDeckHistory))
            return;

        try
        {
            var cards = ExtraDeckRunHistoryDataManager
                .GetExtraDeck(____history, playerIcon.Player.Id)
                .ToList();
            RestoreMissingAcquisitionFloors(____history, playerIcon.Player.Id, cards);
            var unlockState = SaveManager.Instance.GenerateUnlockStateFromProgress();
            var player = Player.CreateForNewRun(
                SaveUtil.CharacterOrDeprecated(playerIcon.Player.Character),
                unlockState,
                playerIcon.Player.Id);

            extraDeckHistory.LoadDeck(player, cards);
            SetExtraDeckHeader(extraDeckHistory, cards);
            extraDeckHistory.Visible = cards.Count > 0;
        }
        catch (NullReferenceException)
        {
            extraDeckHistory.Visible = false;
            BangDreamLibCore.Logger.Info(
                $"Failed to display extra deck for run history player {playerIcon.Player.Id}: Can't find extra deck save data.");
        }
        catch (Exception ex)
        {
            extraDeckHistory.Visible = false;
            BangDreamLibCore.Logger.Warn(
                $"Failed to display extra deck for run history player {playerIcon.Player.Id}: {ex}");
        }
    }

    private static void SetExtraDeckHeader(NDeckHistory extraDeckHistory, IReadOnlyCollection<SerializableCard> cards)
    {
        var rarityCounts = Enum.GetValues<CardRarity>().ToDictionary(rarity => rarity, _ => 0);
        foreach (var card in cards)
        {
            var rarity = SaveUtil.CardOrDeprecated(card.Id!).Rarity;
            rarityCounts[rarity]++;
        }

        var header = new LocString("gameplay_ui", "BANG_DREAM_LIB_EXTRA_DECK_HISTORY.header");
        header.Add("totalCards", cards.Count);
        var categories = new LocString("run_history", "DECK_HISTORY.categories");
        foreach (var (rarity, count) in rarityCounts)
            categories.Add(rarity + "Cards", count);

        var text = new StringBuilder();
        text.Append("[gold][b]");
        text.Append(header.GetFormattedText());
        text.Append("[/b][/gold]");
        text.Append(categories.GetFormattedText().Trim(','));
        extraDeckHistory.GetNode<MegaRichTextLabel>("Header").Text = text.ToString();
    }

    private static void RestoreMissingAcquisitionFloors(
        GameRunHistory history,
        ulong playerId,
        IList<SerializableCard> cards)
    {
        var pickedCardsById = new Dictionary<ModelId,
            Queue<(int FloorNumber, PlayerMapPointHistoryEntry PlayerEntry, SerializableCard Card)>>();
        var floorNumber = 1;
        foreach (var act in history.MapPointHistory)
        {
            foreach (var mapPoint in act)
            {
                var playerEntry = mapPoint.PlayerStats.FirstOrDefault(entry => entry.PlayerId == playerId);
                if (playerEntry != null)
                {
                    var recordedGains = playerEntry.CardsGained.ToList();
                    foreach (var choice in playerEntry.CardChoices.Where(choice => choice.wasPicked))
                    {
                        var recordedGainIndex = recordedGains.FindIndex(card => card.Equals(choice.Card));
                        if (recordedGainIndex >= 0)
                        {
                            recordedGains.RemoveAt(recordedGainIndex);
                            continue;
                        }

                        if (choice.Card.Id is not { } cardId)
                            continue;

                        if (!pickedCardsById.TryGetValue(cardId, out var floors))
                        {
                            floors = new Queue<
                                (int FloorNumber, PlayerMapPointHistoryEntry PlayerEntry, SerializableCard Card)>();
                            pickedCardsById.Add(cardId, floors);
                        }

                        floors.Enqueue((floorNumber, playerEntry, choice.Card));
                    }
                }

                floorNumber++;
            }
        }

        foreach (var card in cards)
        {
            if (card.FloorAddedToDeck.HasValue || card.Id is not { } cardId ||
                !pickedCardsById.TryGetValue(cardId, out var floors) || floors.Count == 0)
                continue;

            var acquisition = floors.Dequeue();
            card.FloorAddedToDeck = acquisition.FloorNumber;
            acquisition.PlayerEntry.CardsGained.Add(acquisition.Card);
        }
    }
}

internal static class ExtraDeckRunHistoryViewState
{
    private static readonly ConditionalWeakTable<NRunHistory, NDeckHistory> Views = new();

    public static void Set(NRunHistory screen, NDeckHistory view)
    {
        Views.Remove(screen);
        Views.Add(screen, view);
    }

    public static bool TryGet(NRunHistory screen, out NDeckHistory view)
    {
        return Views.TryGetValue(screen, out view!);
    }
}