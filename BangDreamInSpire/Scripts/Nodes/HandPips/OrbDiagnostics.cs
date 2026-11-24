using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;

namespace BangDreamInSpire.Scripts.Nodes.HandPips;

internal static partial class OrbRenderer
{
    /// <summary>
    /// 位移探针（Config.Data.Debug）：整排 Root.Position.Y 与首颗球全局 Y 的每帧最大变化，
    /// 1 s 打一行。行首 [RHL probe] 与字段名是核对基准，不要改。
    /// </summary>
    private static void TickProbe(Overlay overlay)
    {
        // 位移探针：整排（Root.Position）与单颗球（pip 全局 Y）每帧最大变化
        double nowProbe = Time.GetTicksMsec() / 1000.0;
        float rootYNow = overlay.Root.Position.Y;
        float pipYNow = overlay.Energy.Pips.Count > 0
            ? overlay.Energy.Pips[0].Root.GlobalPosition.Y
            : 0f;

        if (!float.IsNaN(overlay.ProbeLastRootY))
        {
            overlay.ProbeMaxRootDY = Mathf.Max(overlay.ProbeMaxRootDY,
                Mathf.Abs(rootYNow - overlay.ProbeLastRootY));
            overlay.ProbeMaxPipDY = Mathf.Max(overlay.ProbeMaxPipDY,
                Mathf.Abs(pipYNow - overlay.ProbeLastPipY));
        }

        overlay.ProbeLastRootY = rootYNow;
        overlay.ProbeLastPipY = pipYNow;

        if (nowProbe - overlay.ProbeAt > 1.0)
        {
            overlay.ProbeAt = nowProbe;
            GD.Print($"[RHL probe] free={Config.Data.FreePlace} rootY={rootYNow:F1} pipY={pipYNow:F1} " +
                     $"maxΔroot={overlay.ProbeMaxRootDY:F2}px maxΔpip={overlay.ProbeMaxPipDY:F2}px " +
                     $"scale={overlay.Root.Scale.X:F2} topLevel={overlay.Root.TopLevel} dim={overlay.Dim:F2}");
            overlay.ProbeMaxRootDY = 0f;
            overlay.ProbeMaxPipDY = 0f;
        }
    }

    /// <summary>
    /// 特效状态日志（Config.Data.Debug）：fx 行 1 s 一次；orbs at 行每个玩家只打第一次。
    /// 文本与节流周期是核对基准，不要改。
    /// </summary>
    private static void TickFxLog(Overlay overlay, Player player, Block blockE, int energy, int maxEnergy, int previewCost)
    {
        double nowMs = Time.GetTicksMsec() / 1000.0;
        if (nowMs - _fxLogAt > 1.0)
        {
            _fxLogAt = nowMs;
            int willSpend = 0, gems = 0, sparks = 0;
            foreach (Pip p in overlay.Energy.Pips)
            {
                if (p.WillSpend) willSpend++;
                if (p.Gem != null && p.Gem.Visible) gems++;
            }
            sparks += overlay.Energy.Sparks.Count;
            foreach (Row r in overlay.Secondary.Values) sparks += r.Sparks.Count;
            float r0 = overlay.Energy.Pips.Count > 0 ? overlay.Energy.Pips[0].RippleT : -1f;
            int visE = 0;
            foreach (Pip p in overlay.Energy.Pips)
            {
                if (p.Root.Visible) visE++;
            }

            GD.Print($"[BangDreamInSpire/handpips] fx: willSpend={willSpend} gems={gems} sparks={sparks} " +
                     $"pips={overlay.Energy.Pips.Count} visible={visE} orbSize={blockE.Size:F2} " +
                     $"previewCost={previewCost} r0={r0:F2} hidden={overlay.WasHidden} " +
                     $"root=({overlay.Root.GlobalPosition.X:F0},{overlay.Root.GlobalPosition.Y:F0}) " +
                     $"opacity={overlay.Opacity:F2} dim={overlay.Dim:F2}");
        }

        if (Logged.Add(player))
        {
            GD.Print($"[BangDreamInSpire/handpips] orbs at ({overlay.RowX ?? 0f:F0},{overlay.RowY ?? 0f:F0}) cardsTop={overlay.LastCardsTop:F0} size={blockE.Size:F2} " +
                     $"spacing={blockE.Spacing:F1} energy={energy}/{maxEnergy} rows={blockE.Rows} " +
                     $"secondary={overlay.Secondary.Count} mode={Config.Data.Mode} free={Config.Data.FreePlace}");
        }
    }
}