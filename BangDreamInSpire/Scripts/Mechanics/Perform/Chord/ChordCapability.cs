using System.Text.Json.Nodes;
using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Utils.Infos;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;

namespace BangDreamInSpire.Scripts.Mechanics.Perform.Chord;

/// <summary>
/// 卡牌和弦能力：承载卡牌的演奏和弦，随卡牌一同克隆与存档（每个卡牌实例各自持有）。
/// <para>
/// 和弦只有一个用途——卡牌打出后激活对应分组中音乐牌的奏响（由角色的
/// <see cref="Interfaces.GameHook.IPerformTriggerListener" /> 规则读取），不参与音乐牌的入组判定。
/// </para>
/// <para>
/// 和弦由 <see cref="SetChord" /> 在运行时赋值；当前只有控制台命令与休息处「和弦设计」两处调用，
/// 卡牌奖励流程尚未接入（见 docs/mechanic-authoring.md §9）。
/// </para>
/// </summary>
[RegisterModelCapability]
public sealed class ChordCapability : CardCapability, ICardOverlayContributor
{
    private const string ChordStateKey = "chord";

    /// <summary>书签覆盖层的稳定 ID（同一能力内排序与诊断用，须与 <c>OverlayFactoriesMechanic</c> 的注册键一致）。</summary>
    private const string OverlayId = "chord_bookmark";

    /// <summary>当前和弦；未赋予时为 <see cref="PerformChord.None" />。</summary>
    public PerformChord Chord { get; private set; } = PerformChord.None;

    /// <summary>
    /// 赋予和弦；传入 <see cref="PerformChord.None" /> 表示移除本能力（无和弦）。
    /// </summary>
    public void SetChord(PerformChord chord)
    {
        if (chord == PerformChord.None)
        {
            RemoveFromOwner();
            return;
        }

        if (Chord == chord) return;

        Chord = chord;
        MarkDirty();
    }

    /// <summary>
    /// 卡面覆盖层：在卡牌右边缘画出和弦书签（多和弦时竖直堆叠多枚箭头书签）。
    /// 覆盖层由 RitsuLib 在 <c>NCard.ReloadOverlay</c> 之后重建，因此卡牌模型刷新时书签会自动跟随；
    /// 工厂每次返回新节点（同一张卡可能同时被多个 <c>NCard</c> 显示）。
    /// </summary>
    public IEnumerable<CardOverlayContribution> GetCardOverlays(CardOverlayContext context)
    {
        if (Chord == PerformChord.None)
        {
            yield break;
        }

        var factory = CardOverlayRegistry.Resolve(OverlayId);
        if (factory == null)
        {
            yield break;
        }

        yield return CardOverlayContribution.FromFactory(
            OverlayId,
            factory,
            order: 0,
            fullRect: false);
    }

    /// <inheritdoc />
    protected override JsonNode? SaveAdditionalState()
    {
        return Chord == PerformChord.None
            ? null
            : new JsonObject { [ChordStateKey] = (int)Chord };
    }

    /// <inheritdoc />
    protected override void LoadAdditionalState(JsonNode? state, int schemaVersion)
    {
        Chord = state is JsonObject json && json.TryGetPropertyValue(ChordStateKey, out var node) && node != null
            ? (PerformChord)node.GetValue<int>()
            : PerformChord.None;
    }
}
