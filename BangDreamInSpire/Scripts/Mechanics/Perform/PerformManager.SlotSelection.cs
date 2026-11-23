// 手动落位选择。
using System.Diagnostics.CodeAnalysis;
using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Interfaces.Mechanics;
using BangDreamInSpire.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace BangDreamInSpire.Scripts.Mechanics.Perform;

public partial class PerformManager
{
    /// <summary>计算候选并进入本地选择；非手动目标、无候选或唯一分组时使用常规规则。</summary>
    public void BeginManualSlotSelection(CardModel cardModel)
    {
        ArgumentNullException.ThrowIfNull(cardModel);

        var mode = InsertSongRules.ResolveSlotSelectionMode(cardModel);
        if (mode == PerformSlotSelectionMode.None) return;
        if (NControllerManager.Instance?.IsUsingDirectionalNavigation == true)
        {
            BangDreamInSpireCore.Logger.Info(
                $"Manual slot selection skipped for {cardModel.Id.Entry}: controller navigation.");
            return;
        }

        if (!TryGetPerformArea(out var performArea))
        {
            BangDreamInSpireCore.Logger.Warn(
                $"Manual slot selection skipped for {cardModel.Id.Entry}: perform area is not ready.");
            return;
        }

        var candidates = BuildSlotCandidates();
        if (candidates.Count == 0)
        {
            BangDreamInSpireCore.Logger.Warn(
                $"Manual slot selection skipped for {cardModel.Id.Entry}: no active slot candidate.");
            return;
        }

        // 唯一可达分组无需选择。
        if (mode == PerformSlotSelectionMode.Group &&
            candidates.Select(candidate => candidate.Group).Distinct().Count() <= 1)
        {
            BangDreamInSpireCore.Logger.Info(
                $"Manual slot selection skipped for {cardModel.Id.Entry}: only one reachable group.");
            return;
        }

        performArea.BeginSlotSelection(cardModel, mode, candidates);
        BangDreamInSpireCore.Logger.Info(
            $"Manual slot selection started for {cardModel.Id.Entry} ({mode}) : " +
            $"slots [{string.Join(", ", candidates.Select(candidate => candidate.SlotIndex))}]");
    }

    /// <summary>立即刷新选择指向。</summary>
    public void RefreshManualSlotSelection(CardModel cardModel)
    {
        if (TryGetPerformArea(out var performArea) && performArea.SlotSelectionCard == cardModel)
        {
            performArea.RefreshSlotSelection();
        }
    }

    /// <summary>刷新指向并返回是否有候选。</summary>
    public bool HasManualSlotCandidate(CardModel cardModel)
    {
        if (!TryGetPerformArea(out var performArea) || performArea.SlotSelectionCard != cardModel) return false;

        performArea.RefreshSlotSelection();
        return performArea.HoveredSlotIndex > 0;
    }

    public bool IsManualSlotSelectionActive(CardModel cardModel)
    {
        return TryGetPerformArea(out var performArea) &&
               performArea.IsSlotSelectionActive &&
               performArea.SlotSelectionCard == cardModel;
    }

    /// <summary>结束选择并将命中结果写入 PerformContext.Request。</summary>
    public bool TryConsumeManualSlotSelection(CardModel cardModel, out PerformEnqueueRequest request)
    {
        request = default;
        if (!IsManualSlotSelectionActive(cardModel)) return false;
        if (!TryGetPerformArea(out var performArea)) return false;

        var selected = performArea.TryConsumeSlotSelection();
        if (selected.IsEmpty) return false;

        CardContexts.GetOrCreate(cardModel).Request = selected;
        request = selected;
        return true;
    }

    /// <summary>结束选择；指定卡牌时不打断其他选择。</summary>
    public void EndManualSlotSelection(CardModel? cardModel = null)
    {
        if (!TryGetPerformArea(out var performArea) || !performArea.IsSlotSelectionActive) return;
        if (cardModel != null && performArea.SlotSelectionCard != cardModel) return;

        performArea.EndSlotSelection();
    }

    /// <summary>取消选择并清空未消费的落位请求。</summary>
    public void CancelManualSlotSelection(CardModel cardModel)
    {
        EndManualSlotSelection(cardModel);
        CardContexts.GetOrCreate(cardModel).Request = default;
    }

    /// <summary>返回选择诊断文本。</summary>
    public string DescribeManualSlotSelection(CardModel cardModel)
    {
        return TryGetPerformArea(out var performArea) && performArea.SlotSelectionCard == cardModel
            ? performArea.DescribeSlotSelection()
            : "<no active selection for this card>";
    }

    /// <summary>返回激活槽位与分组的候选集合。</summary>
    private List<PerformSlotCandidate> BuildSlotCandidates()
    {
        // 无和弦的默认分组不作为多分组候选。
        var skipDefaultGroup = _scheme.Groups.Count > 1;
        var candidates = new List<PerformSlotCandidate>();
        foreach (var descriptor in _scheme.Groups)
        {
            if (skipDefaultGroup && descriptor.Chord == PerformChord.None) continue;

            foreach (var slotIndex in _capacity.GetActiveSlots(descriptor.Chord))
            {
                candidates.Add(new PerformSlotCandidate(slotIndex, descriptor.Chord));
            }
        }

        return candidates;
    }

    /// <summary>获取可用的演奏区节点。</summary>
    internal bool TryGetPerformArea([NotNullWhen(true)] out IPerformArea? performArea)
    {
        performArea = PerformArea;
        return performArea is GodotObject godotObject && GodotObject.IsInstanceValid(godotObject);
    }
}
