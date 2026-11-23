using System.Text.Json.Nodes;
using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Utils.Infos;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;

namespace BangDreamInSpire.Scripts.Mechanics.Perform.Chord;

/// <summary>随卡牌克隆与存档的和弦能力，仅决定奏响分组，不影响入组。</summary>
[RegisterModelCapability]
public sealed class ChordCapability : CardCapability, ICardOverlayContributor
{
    private const string ChordStateKey = "chord";

    /// <summary>书签覆盖层 ID，须与工厂注册键一致。</summary>
    private const string OverlayId = "chord_bookmark";

    /// <summary>当前和弦。</summary>
    public PerformChord Chord { get; private set; } = PerformChord.None;

    /// <summary>设置和弦，None 表示移除能力。</summary>
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

    /// <summary>创建和弦书签；每次返回独立节点以支持多卡面。</summary>
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
        // 位值按原始 int 存档，只能追加。
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
