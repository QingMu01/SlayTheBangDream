using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace BangDreamInSpire.Scripts.Nodes.HandPips;

internal static partial class OrbRenderer
{
    /// <summary>
    /// 战斗外看门狗：战斗结束、手牌被移出树、或 PlayerCombatState 变空时把整排藏掉。
    /// 用 SceneTree.ProcessFrame 信号驱动（普通 C# 类，不依赖 Godot 的 _Process/_Draw 派发）。
    /// </summary>
    internal sealed class CombatWatchdog
    {
        public Player Player;
        public NPlayerHand Hand;
        public Node2D Root;
        private SceneTree _tree;

        public void Attach(SceneTree tree)
        {
            _tree = tree;
            if (_tree != null)
            {
                _tree.ProcessFrame += OnFrame;
            }
        }

        private void OnFrame()
        {
            bool alive = Root != null && GodotObject.IsInstanceValid(Root)
                         && Hand != null && GodotObject.IsInstanceValid(Hand);

            if (!alive)
            {
                Detach();
                return;
            }

            try
            {
                bool inCombat = Player != null && Player.PlayerCombatState != null;

                // 选牌 / 卡牌选择界面：手牌节点还在，但已经不是战斗里的手牌了
                bool selecting = false;
                try
                {
                    selecting = Hand.IsInCardSelection;
                }
                catch
                {
                    selecting = false;
                }

                if (!inCombat || selecting || !Hand.IsVisibleInTree())
                {
                    Root.Visible = false;
                }
            }
            catch
            {
                Detach();
            }
        }

        private void Detach()
        {
            if (_tree != null && GodotObject.IsInstanceValid(_tree))
            {
                _tree.ProcessFrame -= OnFrame;
            }

            _tree = null;
        }
    }
}