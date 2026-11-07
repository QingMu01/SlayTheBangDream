using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Mechanics.Perform.Chord;
using BangDreamInSpire.Scripts.Nodes.Screens;
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

namespace BangDreamInSpire.Scripts.Mechanics.Perform.Site;

/// <summary>
/// 火堆（休息处）选项「和弦设计」：打开 <see cref="NChordDesignScreen" />，给卡组里的一张牌设置和弦。
/// <para>
/// 钦定只在睦（<c>WakabaMutsumi</c>）的休息处出现（见 <c>MutsumiSpecialRules.TryModifyRestSiteOptions</c>）。
/// 联机时 <see cref="RestSiteOption.OnSelect" /> 会在每个 peer 上执行，因此这里按
/// <c>ReserveChoiceId / SyncLocalChoice / WaitForRemoteChoice</c> 把本地玩家的选择同步给其他 peer，
/// 再在全部 peer 上以相同参数修改卡牌（与 <c>ExtraPileCmd</c> 同款范式）。
/// </para>
/// </summary>
public class ChordDesignOption(Player owner) : ModRestSiteOptionTemplate(owner)
{
    /// <summary>选项 / 面板本地化表（与本体休息处文案同表，mod 的 rest_site_ui.json 会被合并进来）。</summary>
    private const string LocTable = "rest_site_ui";

    /// <summary>本体 <c>NCard.ReloadOverlay</c> 是私有方法，改完和弦后借它手动重建卡面覆盖层。</summary>
    private static readonly Action<NCard>? ReloadOverlay = AccessTools.DeclaredMethod(typeof(NCard), "ReloadOverlay")
        is { } method
        ? AccessTools.MethodDelegate<Action<NCard>>(method)
        : null;

    public override string OptionId => "BANG_DREAM_IN_SPIRE_CHORD_DESIGN";

    public override string CustomIconPath => "res://BangDreamInSpire/images/sceneui/music_draw.png";

    public override LocString Description => new(
        LocTable,
        $"OPTION_{OptionId}.{(IsEnabled ? "description" : "descriptionDisabled")}"
    );

    public override bool IsEnabled => Owner.Deck.Cards.Count > 0;

    /// <summary>选中并应用后的那张牌；<see cref="DoLocalPostSelectVfx" /> 用它播放「展示 → 飞入卡组」。</summary>
    private CardModel? _appliedCard;

    /// <summary>预载升级式展示/飞行的场景（本体 <see cref="NCardUpgradeVfx" /> 的资产清单）。</summary>
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
            // 场景缺失时面板打不开：按「取消」处理，仍然同步空结果，别让远端一直等（否则会把选项卡死）
            var result = screen == null ? (ChordDesignResult?)null : await screen.SelectionFinished();

            // 取消时也要同步一次空结果，否则远端会一直等在本选项上
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

    /// <summary>把和弦写到卡牌上；<paramref name="additive" /> 为 true 时与卡牌现有和弦取并集。</summary>
    private static void Apply(CardModel card, PerformChord chord, bool additive)
    {
        if (additive && card.TryGetCapability<ChordCapability>(out var existing))
        {
            chord |= existing.Chord;
        }

        card.GetOrCreateCapability<ChordCapability>().SetChord(chord);

        // 卡面角标只在 NCard.ReloadOverlay 后重建；休息处没有手牌，这里防御性地对可能的卡面实例刷新一次
        if (NCard.FindOnTable(card) is { } cardNode)
        {
            ReloadOverlay?.Invoke(cardNode);
        }
    }

    /// <summary>
    /// 与升级卡牌同样的收尾：先在屏幕中央放大展示这张牌（角标已是新和弦），停留片刻后飞入卡组图标。
    /// 展示与飞行都由本体 <see cref="NCardUpgradeVfx" /> 负责（它内部再调 <c>NCardFlyVfx</c>），
    /// 这里只等展示段结束，把飞行留给后场继续播放。
    /// </summary>
    public override async Task DoLocalPostSelectVfx(CancellationToken ct = default)
    {
        if (_appliedCard is not { } card)
            return;

        if (NCardUpgradeVfx.Create(card) is { } vfx)
        {
            NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(vfx);
        }

        // NCardUpgradeVfx：0.25s 放大 + 1.75s 展示，之后起飞；对齐到「展示将结束、飞行刚开始」的时刻
        await Cmd.CustomScaledWait(1.4f, 1.9f, ignoreCombatEnd: false, ct);
    }

    public override Task DoRemotePostSelectVfx() => Task.CompletedTask;
}
