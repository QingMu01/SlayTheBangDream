using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Mechanics.Perform.Chord;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Models.Capabilities;
using STS2RitsuLib.Scaffolding.Content;

namespace BangDreamInSpire.Scripts.Nodes.Screens;

/// <summary>休息处和弦设计选项；本地选择同步到各端后统一修改卡牌。</summary>
public class ChordDesignOption(Player owner) : ModRestSiteOptionTemplate(owner)
{
    private const string LocTable = "rest_site_ui";

    /// <summary>反射重建卡面覆盖层。</summary>
    private static readonly Action<NCard>? ReloadOverlay = AccessTools.DeclaredMethod(typeof(NCard), "ReloadOverlay")
        is { } method
        ? AccessTools.MethodDelegate<Action<NCard>>(method)
        : null;

    public override string OptionId => "BANG_DREAM_IN_SPIRE_CHORD_DESIGN";

    public override string CustomIconPath => "res://BangDreamInSpire/images/sceneui/music_draw.png";

    // IsEnabled 与 Description 首次求值后缓存。
    private bool? _isEnabled;
    private LocString? _description;

    public override LocString Description => _description ??= new(
        LocTable,
        $"OPTION_{OptionId}.{(IsEnabled ? "description" : "descriptionDisabled")}"
    );

    public override bool IsEnabled => _isEnabled ??= Owner.Deck.Cards.Count > 0;

    /// <summary>当前修改的卡牌。</summary>
    private CardModel? _appliedCard;

    /// <summary>升级展示与飞行场景。</summary>
    public override IEnumerable<string> AssetPaths => base.AssetPaths.Concat(NCardUpgradeVfx.AssetPaths);

    public override async Task<bool> OnSelect()
    {
        var deck = Owner.Deck.Cards;
        if (deck.Count == 0)
            return false;

        var choiceId = RunManager.Instance.PlayerChoiceSynchronizer.ReserveChoiceId(Owner);

        int cardIndex;
        PerformChord chord;
        bool additive;

        if (LocalContext.IsMe(Owner) && RunManager.Instance.NetService.Type != NetGameType.Replay)
        {
            var screen = NChordDesignScreen.ShowScreen(deck);
            var result = screen == null ? (ChordDesignResult?)null : await screen.SelectionFinished();

            // 取消或场景缺失时同步空结果，避免远端一直等待本选项。
            var indexes = result is { } chosen
                ? new List<int> { chosen.CardIndex, (int)chosen.Chord, chosen.Additive ? 1 : 0 }
                : new List<int>();
            RunManager.Instance.PlayerChoiceSynchronizer.SyncLocalChoice(
                Owner, choiceId, PlayerChoiceResult.FromIndexes(indexes));

            if (result is not { } confirmed)
                return false;

            (cardIndex, chord, additive) = (confirmed.CardIndex, confirmed.Chord, confirmed.Additive);
        }
        else
        {
            var indexes = (await RunManager.Instance.PlayerChoiceSynchronizer
                .WaitForRemoteChoice(Owner, choiceId)).AsIndexes();
            if (indexes.Count < 3)
                return false;

            cardIndex = indexes[0];
            chord = (PerformChord)indexes[1];
            additive = indexes[2] != 0;
        }

        if (cardIndex < 0 || cardIndex >= deck.Count || chord == PerformChord.None)
            return false;

        Apply(deck[cardIndex], chord, additive);
        _appliedCard = deck[cardIndex];
        return true;
    }

    /// <summary>设置和弦，additive 表示取并集。</summary>
    private static void Apply(CardModel card, PerformChord chord, bool additive)
    {
        if (additive && card.TryGetCapability<ChordCapability>(out var existing))
        {
            chord |= existing.Chord;
        }

        card.GetOrCreateCapability<ChordCapability>().SetChord(chord);

        // 对已有卡面刷新覆盖层。
        if (NCard.FindOnTable(card) is { } cardNode)
        {
            ReloadOverlay?.Invoke(cardNode);
        }
    }

    /// <summary>播放升级式展示与飞入卡组动画。</summary>
    public override async Task DoLocalPostSelectVfx(CancellationToken ct = default)
    {
        if (_appliedCard is not { } card)
            return;

        if (NCardUpgradeVfx.Create(card) is { } vfx)
        {
            NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(vfx);
        }

        // 对齐展示结束与飞行开始。
        await Cmd.CustomScaledWait(1.4f, 1.9f, ignoreCombatEnd: false, ct);
    }

    public override Task DoRemotePostSelectVfx() => Task.CompletedTask;
}
