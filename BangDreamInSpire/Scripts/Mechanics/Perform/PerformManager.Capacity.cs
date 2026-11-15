// Capacity and scheme handling for PerformManager, split out from PerformManager.cs.
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
        private IPerformScheme _scheme = EmptyPerformScheme.Instance;

        private PerformCapacityState _capacity = new(EmptyPerformScheme.Instance);

        /// <summary>当前已激活的演奏槽位总数。</summary>
        public int Capacity => _capacity.ActiveSlotCount;

        /// <summary>全部演奏分组的容量上限之和。</summary>
        public int MaxCapacity => _capacity.TotalSlotCount;

        /// <summary>角色提供的演奏方案（不可变策略）。</summary>
        public IPerformScheme Scheme => _scheme;

        /// <summary>
        /// 增加演奏容量。未指定和弦分组时对全部分组生效。
        /// </summary>
        public void AddCapacity(int amount, PerformChord? chord = null)
        {
            if (amount <= 0) return;
    
            switch (_capacity.Add(amount, chord))
            {
                case PerformCapacityChangeResult.Applied:
                    SyncPerformArea();
                    break;
                case PerformCapacityChangeResult.RejectedAtMax:
                    ThinkCmd.Play(MaxSizeThink, Player.Creature, 1.5d);
                    break;
                case PerformCapacityChangeResult.Ignored:
                default:
                    break;
            }
        }

        /// <summary>
        /// 减少演奏容量。未指定和弦分组时对全部分组生效。
        /// </summary>
        public void ReduceCapacity(int amount, PerformChord? chord = null)
        {
            if (amount <= 0) return;
            _capacity.Reduce(amount, chord);
            SyncPerformArea();
            TaskHelper.RunSafely(RemoveOverflowItems());
        }

        private void SyncPerformArea()
        {
            PerformArea.SyncSlots(_capacity.TotalSlotCount, _capacity.ActiveSlots);
        }
}
