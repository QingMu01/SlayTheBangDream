using System.Text.Json.Nodes;
using BangDreamLib.Scripts.Enums;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;

namespace BangDreamLib.Scripts.Mechanics.Perform.Chord;

/// <summary>
/// 卡牌和弦能力：承载卡牌的演奏和弦，随卡牌一同克隆与存档（每个卡牌实例各自持有）。
/// <para>
/// 和弦只有一个用途——卡牌打出后激活对应分组中音乐牌的奏响（由角色的
/// <see cref="Interfaces.GameHook.IPerformTriggerListener" /> 规则读取），不参与音乐牌的入组判定。
/// </para>
/// <para>
/// 和弦在卡牌奖励生成时随机赋予，见 <see cref="SetChord" />；没有设计期或静态赋值入口。
/// </para>
/// </summary>
[RegisterModelCapability]
public sealed class ChordCapability : CardCapability
{
    private const string ChordStateKey = "chord";

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
