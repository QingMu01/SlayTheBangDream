// Manual slot-selection handling for PerformManager, split out from PerformManager.cs.
using System.Text.Json;
using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Extensions;
using BangDreamInSpire.Scripts.Interfaces;
using BangDreamInSpire.Scripts.Interfaces.CardAugment;
using BangDreamInSpire.Scripts.Interfaces.CharacterAugment;
using BangDreamInSpire.Scripts.Interfaces.GameHook;
using BangDreamInSpire.Scripts.Interfaces.Mechanics;
using BangDreamInSpire.Scripts.Mechanics.Perform.Schemes;
using BangDreamInSpire.Scripts.Utils;
using BangDreamInSpire.Scripts.Utils.Infos;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;
using STS2RitsuLib.Networking.ManagedActions;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;
using STS2RitsuLib.Utils;

namespace BangDreamInSpire.Scripts.Mechanics.Perform;

public partial class PerformManager
{
        /// <summary>
        /// 开始一次手动落位选择：按目标类型计算候选槽位，并让本地演奏区域进入选择态。
        /// 非手动落位类型、手柄方向导航、无候选槽位（容量为 0 或演奏区域未就绪）、
        /// 指定分组但只有一个可达分组时都不进入选择态，出牌按卡牌自身的入队规则处理。
        /// </summary>
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
    
            // 指定分组模式下只有一个可达分组时无从选择，其落位等价于常规规则，直接不进入选择态。
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

        /// <summary>
        /// 按当前鼠标位置立即刷新一次选择指向（松手瞬间判定用）。
        /// </summary>
        public void RefreshManualSlotSelection(CardModel cardModel)
        {
            if (TryGetPerformArea(out var performArea) && performArea.SlotSelectionCard == cardModel)
            {
                performArea.RefreshSlotSelection();
            }
        }

        /// <summary>
        /// 该牌的手动落位选择当前是否指向候选槽位（判定前先按当前鼠标位置刷新一次指向）。
        /// </summary>
        public bool HasManualSlotCandidate(CardModel cardModel)
        {
            if (!TryGetPerformArea(out var performArea) || performArea.SlotSelectionCard != cardModel) return false;
    
            performArea.RefreshSlotSelection();
            return performArea.HoveredSlotIndex > 0;
        }

        /// <summary>该卡当前是否正处于手动落位选择态（未进入选择态时出牌走常规规则）。</summary>
        public bool IsManualSlotSelectionActive(CardModel cardModel)
        {
            return TryGetPerformArea(out var performArea) &&
                   performArea.IsSlotSelectionActive &&
                   performArea.SlotSelectionCard == cardModel;
        }

        /// <summary>
        /// 结束手动落位选择并取出结果；返回 <see langword="false" /> 表示未命中任何候选槽位。
        /// 命中时把结果写入该牌的 <see cref="PerformContext.Request" />，供入队规划与网络动作读取。
        /// </summary>
        public bool TryConsumeManualSlotSelection(CardModel cardModel, out PerformEnqueueRequest request)
        {
            request = default;
            if (!IsManualSlotSelectionActive(cardModel)) return false;
    
            var selected = PerformArea.TryConsumeSlotSelection();
            if (selected.IsEmpty) return false;
    
            CardContexts.GetOrCreate(cardModel).Request = selected;
            request = selected;
            return true;
        }

        /// <summary>
        /// 结束选择态与槽位高亮。<paramref name="cardModel" /> 非空时只结束该牌自己的选择：
        /// 其他卡牌出牌收尾不应打断正在进行的落位选择。
        /// </summary>
        public void EndManualSlotSelection(CardModel? cardModel = null)
        {
            if (!TryGetPerformArea(out var performArea) || !performArea.IsSlotSelectionActive) return;
            if (cardModel != null && performArea.SlotSelectionCard != cardModel) return;
    
            performArea.EndSlotSelection();
        }

        /// <summary>取消手动落位选择，并清空该牌尚未消费的落位请求。</summary>
        public void CancelManualSlotSelection(CardModel cardModel)
        {
            EndManualSlotSelection(cardModel);
            CardContexts.GetOrCreate(cardModel).Request = default;
        }

        /// <summary>选择态诊断文本；该牌不在选择态时返回占位说明。</summary>
        public string DescribeManualSlotSelection(CardModel cardModel)
        {
            return TryGetPerformArea(out var performArea) && performArea.SlotSelectionCard == cardModel
                ? performArea.DescribeSlotSelection()
                : "<no active selection for this card>";
        }

        /// <summary>本角色全部已激活槽位及其所属和弦分组，作为手动选择的候选集合。</summary>
        private List<PerformSlotCandidate> BuildSlotCandidates()
        {
            // 分组和弦为 PerformChord.None 表示"唯一的默认分组"：其落位与常规规则等价，
            // 且无法用和弦表达落位请求，因此多分组方案下不作为候选（留待常规规则处理）。
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

        private bool TryGetPerformArea(out IPerformArea performArea)
        {
            performArea = PerformArea;
            return performArea is GodotObject godotObject && GodotObject.IsInstanceValid(godotObject);
        }
}
