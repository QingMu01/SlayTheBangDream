using BangDreamLib.Scripts.Enums;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BangDreamLib.Scripts.Interfaces.CharacterAugment;

/// <summary>
/// 槽位在演奏区域内的锚点方位。
/// </summary>
public enum PerformSlotCorner
{
    /// <summary>命中框水平居中，自命中框底边向上堆叠、并逐格朝玩家靠近的竖排（祥子）。</summary>
    BottomCenter,

    /// <summary>命中框左上角，沿朝内方向堆叠。</summary>
    TopLeft,

    /// <summary>命中框左下角，沿朝内方向堆叠。</summary>
    BottomLeft,

    /// <summary>命中框右上角，沿朝内方向堆叠。</summary>
    TopRight,

    /// <summary>命中框右下角，沿朝内方向堆叠。</summary>
    BottomRight
}

/// <summary>
/// 单个槽位的布局锚点。仅为归一化拓扑描述，像素换算与缓动由 <c>NPerformArea</c> 负责。
/// </summary>
/// <param name="Corner">槽位所属的锚点方位。</param>
/// <param name="Offset">该槽位在其所属分组内的 0 基序号。</param>
public readonly record struct PerformSlotAnchor(PerformSlotCorner Corner, int Offset);

/// <summary>
/// 演奏区域相对角色的摆放基准。
/// </summary>
public enum PerformAreaAnchor
{
    /// <summary>角色命中框左侧（祥子）。</summary>
    LeftOfHitbox,

    /// <summary>角色命中框中心（槽位环绕命中框的角色使用）。</summary>
    HitboxCenter
}

/// <summary>
/// 演奏区域相对角色命中框的摆放规则。
/// </summary>
/// <param name="Anchor">摆放基准。</param>
/// <param name="HorizontalMargin">
/// 基准为 <see cref="PerformAreaAnchor.LeftOfHitbox" /> 时，额外向外偏移的像素距离；其余基准忽略。
/// </param>
public readonly record struct PerformAreaPlacement(PerformAreaAnchor Anchor, float HorizontalMargin);

/// <summary>
/// 一个演奏分组的定义。分组即一组共享容量上限的槽位：祥子只有一个分组，
/// 睦则拥有 C/D/F/G 四个和弦分组。
/// </summary>
/// <param name="Chord">本分组对应的和弦。分组不区分和弦时使用 <see cref="PerformChord.None" />。</param>
/// <param name="BaseSlotIndex">本分组占用的全局槽位块起始索引（1 基）。</param>
/// <param name="MaxSlots">本分组的容量上限。</param>
/// <param name="DefaultSlots">本分组在战斗开始时的初始容量。</param>
/// <param name="IsDefault">未手动指定分组时，卡牌是否归入本分组（与 <c>DefaultGroup</c> 同义）。</param>
public readonly record struct PerformGroupDescriptor(
    PerformChord Chord,
    int BaseSlotIndex,
    int MaxSlots,
    int DefaultSlots,
    bool IsDefault = false);

/// <summary>
/// 角色的演奏方案：不可变的静态策略，描述分组划分、容量上限与初值、布局拓扑以及槽位提示来源。
/// 每场战斗的可变容量由 <c>PerformCapacityState</c> 承载，不得存放在方案实例上，
/// 否则多名玩家选择同一角色时会互相污染容量。
/// </summary>
public interface IPerformScheme
{
    /// <summary>全部分组，按 <see cref="PerformGroupDescriptor.BaseSlotIndex" /> 升序。</summary>
    IReadOnlyList<PerformGroupDescriptor> Groups { get; }

    /// <summary>
    /// 未手动指定分组时卡牌归入的分组和弦（和弦本身不参与入组判定）。
    /// </summary>
    PerformChord DefaultGroup { get; }

    /// <summary>全部分组的容量上限之和，即全局槽位索引的取值上界。</summary>
    int TotalSlotCount { get; }

    /// <summary>演奏区域相对角色的摆放规则。</summary>
    PerformAreaPlacement AreaPlacement { get; }

    /// <summary>
    /// 是否需要演奏槽位提示（祥子的余音指示）。返回 <see langword="false" /> 时演奏区域
    /// 不会订阅二级资源变化，也不会显示提示图元。
    /// </summary>
    bool UsesSlotHint { get; }

    /// <summary>取指定全局槽位的布局锚点；索引越界时返回 <see cref="PerformSlotCorner.BottomCenter" /> 锚点。</summary>
    PerformSlotAnchor GetSlotAnchor(int slotIndex);

    /// <summary>
    /// 取指定槽位的显示颜色。由角色决定配色依据：祥子按卡牌的即兴／演奏区分，
    /// 睦按槽位所属的和弦分组区分。
    /// </summary>
    /// <param name="slotIndex">全局槽位索引。</param>
    /// <param name="card">槽位内的卡牌；空槽位为 <see langword="null" />。</param>
    Color GetSlotColor(int slotIndex, CardModel? card);

    /// <summary>
    /// 取槽位提示的目标值。不使用槽位提示的方案应返回 <see langword="false" />。
    /// </summary>
    bool TryGetHintAmount(Player player, out int amount);
}
