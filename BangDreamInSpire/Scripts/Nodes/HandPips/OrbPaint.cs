using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace BangDreamInSpire.Scripts.Nodes.HandPips;

internal static partial class OrbRenderer
{
    private static void PaintRow(Overlay overlay, Row row, int total, int lit, float rowOffset,
        Block block, float xOffset, float delta, int previewCost)
    {
        row.YOffset = rowOffset;
        row.XOffset = xOffset;
        row.LastWidth = block.Width;
        row.LastOrbPx = 46f * block.Size; // 粒子/波纹统一用真实球径，别各自按配置重算

        // 自由摆放：待机浮动 + 待机缩放呼吸整块关掉（位置、缩放都必须钉死，只有压暗可以随回合变）。
        // 非自由模式 idleK 恒为 1，行为一字不改。
        float idleK = Config.Data.FreePlace ? 0f : 1f;


        // 余音行新增：等球建好后，只给新增的那几颗补波纹（延后 0.12 s，避开出现缩放）
        if (row.RippleFrom >= 0)
        {
            int startR = Mathf.Clamp(row.RippleFrom, 0, row.Pips.Count);
            // 只作用于新点亮的那几颗：上界是 lit，不是整排（暗格不能跟着亮）
            int endR = Math.Max(0, Math.Min(lit, row.Pips.Count));
            for (int i = startR; i < endR; i++)
            {
                row.Pips[i].RippleT = -0.12f;
                row.Pips[i].Flash = 1f;
                row.Pips[i].Gain = 1f;
                row.Pips[i].WhiteHit = 1f;
                row.Pips[i].PlusT = 1f;
            }
            row.RippleFrom = -1;
        }

        // 预测值防抖：连续 50 ms 一致才切换（避免悬停抖动导致 ghost 闪烁/跳位）
        if (row.PendingIncoming != row.Incoming)
        {
            row.IncomingHold += delta;
            // 归零要更长的确认：悬停读值短暂丢几帧焦点是常态，不能让 ghost 立刻退场
            //（每次"送走"＝行尾一次 0.32 s 退场动画）
            float holdNeed = row.PendingIncoming == 0 ? 0.10f : 0.05f;
            if (row.IncomingHold >= holdNeed)
            {
                row.Incoming = row.PendingIncoming;
                row.IncomingHold = 0f;
            }
        }
        else
        {
            row.IncomingHold = 0f;
        }

        if (Config.Data.Debug && row.LastLoggedIncoming != row.Incoming)
        {
            row.LastLoggedIncoming = row.Incoming;
            GD.Print($"[BangDreamInSpire/handpips] incoming '{row.Key}': {row.Incoming} pending={row.PendingIncoming} lit={lit} total={total} pips={row.Pips.Count}");
        }

        int baseCount = Math.Clamp(total - row.Incoming, 0, MaxPips);
        int desired = row.Texture == null ? 0 : Math.Clamp(total, 0, MaxPips);

        // ghost 只允许落在 [lit, desired)：顶替暗格，不覆盖亮球、不越过行尾
        int ghostFrom = Mathf.Clamp(Mathf.Max(baseCount, lit), 0, desired);

        int cols = Mathf.Max(1, block.Cols);

        while (row.Pips.Count < desired)
        {
            // 新球（能量/余音都算）：波纹 + 各自的星/音符
            Pip createdPip = NewPip(row);
            if (createdPip == null)
            {
                break; // 父节点无效：这一帧不再建球，避免死循环
            }

            createdPip.RippleT = -0.12f;
            row.Pips.Add(createdPip);
        }

        int litLast = Math.Max(0, Math.Min(lit, Math.Min(desired, row.Pips.Count)));

        int plusFrom = -1;
        if (row.PendingPlus > 0)
        {
            // 只记区间，等这一帧把球位置算完再生成（否则新球位置还是 (0,0)）
            plusFrom = row.GainStart >= 0 ? row.GainStart : Math.Max(0, desired - row.PendingPlus);
            row.PendingPlus = 0;
        }

        if (row.PendingGain > 0)
        {
            // 同样只作用于"新点亮"的那几颗
            int first = row.GainStart >= 0 ? row.GainStart : Math.Max(0, desired - row.PendingGain);
            for (int i = first; i < litLast; i++)
            {
                row.Pips[i].Gain = 1f;
            }

            row.PendingGain = 0;
        }

        for (int i = 0; i < row.Pips.Count; i++)
        {
            Pip pip = row.Pips[i];
            pip.Appear = Mathf.Min(1f, pip.Appear + delta / 0.38f);
            pip.RippleT += delta;
            pip.Flash = Mathf.Max(0f, pip.Flash - delta / 0.35f);
            pip.WhiteHit = Mathf.Max(0f, pip.WhiteHit - delta / 0.12f);
            // 刷新到账那一瞬间就收掉（0.12s ≈ 7 帧），回到"立刻消失"的手感
            pip.PlusT = Mathf.Max(0f, pip.PlusT - delta / 0.12f);

            bool visible = i < desired;
            if (!visible)
            {
                pip.Root.Visible = false;
                continue;
            }

            pip.Root.Visible = true;

            bool nowLit = i < lit;


            if (!pip.Initialized)
            {
                pip.Initialized = true;
                pip.LitTarget = nowLit;
            }
            else if (!nowLit && pip.LitTarget)
            {
                // 只有"本来是亮的、现在灭了"才算消耗
                pip.Burst = 1f;
                if (Config.Data.ConsumeBurst)
                {
                    SpawnBurst(row, pip.Root.Position, Config.Data.BurstCount);
                }
            }

            // 刷新不再用闪光，改由行内扫描带表现（见 PaintRow 里的 Scan）
            int remaining = Math.Max(0, lit - previewCost);
            int missing = Math.Max(0, previewCost - lit);
            pip.WillSpend = previewCost > 0
                            && ((i >= remaining && i < lit) || (i >= lit && i - lit < missing));
            pip.Lacking = pip.WillSpend && i >= lit;
            pip.Incoming = i >= ghostFrom;

            // 铁则 §5：ghost 当帧清空，不做退场动画。
            // （照抄原版的 0.32s 膨胀退场正是"行尾闪一下"的来源：退场那帧会按暗格重排到行尾）

            pip.PreviewK = Mathf.MoveToward(pip.PreviewK, pip.WillSpend ? 1f : 0f, delta / 0.14f);

            pip.LitTarget = nowLit;
            pip.Lit = Mathf.MoveToward(pip.Lit, nowLit ? 1f : 0f, delta / 0.12f);
            pip.Gain = Mathf.Max(0f, pip.Gain - delta / 1.1f);
            pip.Burst = Mathf.Max(0f, pip.Burst - delta / 0.45f);

            // 显示顺序：亮球 → 预测 ghost（占掉暗格位置）→ 剩余暗球
            int incomingCount = desired - ghostFrom;
            int slot = i;
            if (pip.Incoming)
            {
                slot = Mathf.Clamp(lit + (i - ghostFrom), lit, Mathf.Max(lit, desired - 1));
            }
            else if (i >= lit && i < ghostFrom)
            {
                slot = i + incomingCount;
            }

            // ── 多行：按 slot 折行，第 r 行向上堆一个行高 ──
            slot = Math.Max(0, slot);
            int rowIdx = slot / cols;
            int col = slot % cols;
            int colsThisRow = Math.Min(cols, desired - rowIdx * cols);
            if (colsThisRow < 1)
            {
                colsThisRow = 1;
            }

            float pipX = -(colsThisRow - 1) * block.Spacing * 0.5f + col * block.Spacing;
            float halfSpan2 = Math.Max(0.01f, (colsThisRow - 1) * block.Spacing * 0.5f);
            float nx = pipX / halfSpan2;
            // 只有被探头抬起来的时候才弯（平常保持直线）
            float fanLift = row.Fan > 0.5f ? 0.30f * (46f * block.Size) * (1f - nx * nx) : 0f;
            pip.Root.Position = new Vector2(pipX + xOffset, rowOffset - rowIdx * block.Step - fanLift);

            // ── 静止球快路径（位置已写完）────────────────────────────────
            // 只在「这一颗球完全静止」时成立：自由摆放（idleK=0，没有浮动/呼吸）、出现动画结束、
            // 所有一次性动画量归零、不是预测/消耗/缺口、波纹播完、亮灭到位。
            // 这种球 PaintPip 每帧写出来的都是同一批值，跳过不影响观感。
            // Still 记录「上一帧已经是同一副静止状态」：进入静止的第一帧仍然走完整绘制，
            // 由它把环/加号/高光/着色器参数收到终值；BakedSize 保证球径变化（换行/改球径）后重绘一次。
            bool stillNow = idleK <= 0.001f
                            && pip.Appear >= 1f
                            && pip.Flash <= 0.001f
                            && pip.Gain <= 0.001f
                            && pip.Burst <= 0.001f
                            && pip.WhiteHit <= 0.001f
                            && pip.PlusT <= 0.001f
                            && !pip.Incoming && !pip.WillSpend && !pip.Lacking
                            && pip.PreviewK <= 0.001f
                            && pip.RippleT > 1f
                            && (pip.Lit <= 0.001f || pip.Lit >= 0.999f)
                            && Mathf.Abs(pip.BakedSize - block.Size) <= 0.0005f;
            pip.Still = stillNow ? Math.Min(pip.Still + 1, 2) : 0;
            if (pip.Still >= 2)
            {
                continue;
            }

            pip.BakedSize = block.Size;
            PaintPip(overlay.Time, pip, block.Size, row.Glow, idleK);
        }

        if (row.SparkRequest)
        {
            row.SparkRequest = false;
            if (Config.Data.GainEffects)
            {
                int from = row.GainStart >= 0 ? row.GainStart : 0;
                SpawnSparks(row, from, litLast);
            }

            row.GainStart = -1;
        }

        // 余音被消耗：在消耗位置炸开粒子（不留暗格）
        if (row.CashOutPending > 0)
        {
            int payouts = row.CashOutPending;
            row.CashOutPending = 0;
            // 兑现：整排白闪一次 + 双倍碎屑（颜色就是这一行的主色，余音是金色）
            for (int k = 0; k < payouts; k++)
            {
                SpawnNoteBurst(row, new Vector2(row.XOffset, row.YOffset),
                    Mathf.Clamp(Config.Data.BurstCount / 2, 6, 14));
            }
            foreach (Pip cashPip in row.Pips)
            {
                if (cashPip.Root.Visible)
                {
                    cashPip.WhiteHit = 1f;
                }
            }
        }

        if (row.ConsumeFrom >= 0)
        {
            int from = Mathf.Clamp(row.ConsumeFrom, 0, row.Pips.Count);
            int count = Mathf.Max(1, row.ConsumeCount);
            if (Config.Data.ConsumeBurst)
            {
                for (int i = from; i < Mathf.Min(from + count, row.Pips.Count); i++)
                {
                    SpawnBurst(row, row.Pips[i].Root.Position, Config.Data.BurstCount);
                }
            }

            row.ConsumeFrom = -1;
            row.ConsumeCount = 0;
        }

        UpdateBurst(row, delta);
        UpdateSparks(row, delta);
    }

    private static void PaintPip(float time, Pip pip, float size, Color glowColor, float idleK)
    {
        float pk = Mathf.Clamp(pip.PreviewK, 0f, 1f);
        float appear = pip.Appear;
        float appearScale = EaseOutBack(appear);
        float alpha = Mathf.Min(1f, appear * 2f);

        float lit = pip.Lit;
        // incoming 的 ghost 球是「还没到手但已确定」的量：按满亮、原尺寸画，不当成已消耗
        float shell = pip.Incoming ? 0f : (1f - lit);
        float burst = pip.Burst;
        float gain = Config.Data.GainBreath ? pip.Gain : 0f;

        float pulse = pip.WillSpend ? 0.5f + 0.5f * Mathf.Sin(time * 6.5f) : 0f;
        float idle = 0.5f + 0.5f * Mathf.Sin(time * 2.2f + pip.Seed * 0.9f);

        float scale = appearScale * Mathf.Lerp(1f, 0.66f, shell);
        // ghost 与亮球同尺寸（原版 scale = size，不缩）
        scale *= 1f + 0.02f * idle * lit * idleK;
        if (burst > 0f)
        {
            scale *= 1f + 0.12f * Mathf.Sin(Mathf.Pi * burst);
        }

        if (pip.WillSpend)
        {
            scale *= 1f + 0.08f * pulse;
        }

        float bobAmp = idleK * (Config.Data.IdleBob ? Mathf.Clamp(Config.Data.IdleBobPixels, 0f, 12f) : 0f);
        if (bobAmp > 0.001f)
        {
            pip.Root.Position += new Vector2(0f, Mathf.Sin(time * 2.2f + pip.Seed * 0.9f) * bobAmp);
        }
        pip.Root.Scale = Vector2.One * (scale * size);

        // 该角色能量图标的主色调（略提亮），所有「亮起来」的光都用它，不用纯白
        Color accent = glowColor.Lerp(Colors.White, 0.3f);

        // 付不起的牌：缺的那几颗变灰 + 红色脉冲，消耗波纹也转红（原版 "不够扣的标红"）
        // 缺口（休止不会响）改用深蓝脉冲：红色=危险会误导，蓝色=资源不足但不阻挡出牌
        Color lackRed = new Color(0.24f, 0.44f, 1f);
        Color ringColor = pip.Lacking ? lackRed : glowColor;

        Color tint = glowColor;
        if (gain > 0f)
        {
            // 回能：整颗被绿光包住（呼吸），像录屏里那样
            float breath = 0.75f + 0.25f * Mathf.Sin(time * 5.5f);
            tint = tint.Lerp(GainGlow, gain * breath);
        }

        if (pk > 0.001f)
        {
            Color spendTint = pip.Lacking ? new Color(0.55f, 0.55f, 0.60f) : accent;
            tint = tint.Lerp(spendTint, pk);
        }

        bool ghost = pip.Incoming && gain <= 0.01f;
        if (ghost)
        {
            // 预测 ghost：绿色（原版用 Tone 着色，见下）
            tint = new Color(0.32f, 0.72f, 0.36f);
        }

        // 波纹：即将消耗 → 循环；刷新/新球 → 一次性（同样大小）
        if (pip.Ring != null)
        {
            float p;
            bool show;
            if (pip.WillSpend)
            {
                p = Mathf.PosMod(time / 1.2f + pip.Seed * 0.08f, 1f);
                show = false; // 待机不再用外扩实环，改用内收虚线环
            }
            else
            {
                p = pip.RippleT / 0.85f; // 比待机那圈更快
                show = Config.Data.GainEffects && p >= 0f && p <= 1f;
            }

            if (pip.Incoming)
            {
                show = false;
            }

            if (show)
            {
                float fade = p < 0.03f ? p / 0.03f : Mathf.Pow(1f - (p - 0.03f) / 0.97f, 1.4f);
                float a = 0.95f * fade;
                float ease = 1f - Mathf.Pow(1f - p, 2.2f);
                float rippleMax = Mathf.Clamp(Config.Data.RippleScale, 1.1f, 4.5f);
                float diameter = (46f * size) * (1.02f + Mathf.Max(0f, rippleMax - 1.02f) * ease);

                // 环挂在球节点下，会被球的出现缩放带着一起缩；这里反算补偿，保证世界尺寸稳定
                float rs = Mathf.Max(0.05f, pip.Root.Scale.X);
                pip.Ring.Visible = a > 0.01f;
                pip.Ring.Modulate = new Color(ringColor, a);
                pip.Ring.Scale = Vector2.One * (diameter / (104f * rs));

                if (Config.Data.Debug && a > 0.3f && p < 0.5f)
                {
                    double tnow = Time.GetTicksMsec() / 1000.0;
                    if (tnow - _ringLog > 0.5)
                    {
                        _ringLog = tnow;
                        string src = pip.WillSpend ? "willspend" : "oneshot";
                        GD.Print($"[BangDreamInSpire/handpips] ring[{src}]: p={p:F2} a={a:F2} dia={diameter:F1} " +
                                 $"rippleT={pip.RippleT:F2} rootScale={pip.Root.Scale.X:F2}");
                    }
                }
            }
            else
            {
                pip.Ring.Visible = false;
            }

            // 消耗待机：三层"波纹拖影"（相位错开向外扩，越靠后越淡）
            if (pip.DashRing != null)
            {
                if (pip.WillSpend)
                {
                    float rs2 = Mathf.Max(0.05f, pip.Root.Scale.X);
                    float orbPx3 = 46f * size;
                    float baseP = Mathf.PosMod(time / 1.2f + pip.Seed * 0.08f, 1f);

                    float echoMax = 2.05f * (Mathf.Clamp(Config.Data.RippleScale, 1.1f, 4.5f) / 2.8f);
                    float echoBoost = pip.Lacking ? 1.35f : 1f;
                    DrawEcho(pip.Ring, baseP, 1.0f * echoBoost * pk, orbPx3, rs2, ringColor, echoMax);
                    DrawEcho(pip.EchoMid, baseP - 0.18f, 0.55f * echoBoost * pk, orbPx3, rs2, ringColor, echoMax);
                    DrawEcho(pip.DashRing, baseP - 0.36f, 0.3f * echoBoost * pk, orbPx3, rs2, ringColor, echoMax);
                }
                else
                {
                    pip.DashRing.Visible = false;
                    if (pip.EchoMid != null)
                    {
                        pip.EchoMid.Visible = false;
                    }
                }
            }
        }


        // G1+G2：只有「即将被消耗」的球才有宝石星芒 + 流光
        if (pip.Gem != null)
        {
            pip.Gem.Visible = pk > 0.02f;
            if (pip.Gem.Visible)
            {
                float sp = 0.55f + 0.45f * Mathf.Sin(time * 2.4f + pip.Seed);
                float orbPx2 = 46f * size;
                float parentScale = Mathf.Max(0.01f, pip.Root.Scale.X);
                float gemPx = orbPx2 * 0.78f * (0.85f + 0.25f * sp);
                pip.Gem.Scale = Vector2.One * (gemPx / (64f * parentScale));
                pip.Gem.Rotation = time * 0.9f + pip.Seed;
                pip.Gem.Position = new Vector2(orbPx2 * 0.34f, -orbPx2 * 0.34f) / parentScale;
                pip.Gem.Modulate = new Color(pip.Lacking ? lackRed : Colors.White.Lerp(accent, 0.35f), (0.7f + 0.3f * sp) * pk);
            }
        }

        // 刷新一次：主色外缘闪光（0.35 s 扩散淡出）
        if (pip.Edge != null)
        {
            if (pip.Flash > 0f && Config.Data.GainEffects)
            {
                float f = pip.Flash;
                float orbPxE = 46f * size;
                float d = orbPxE * (1.15f + 0.5f * (1f - f));
                pip.Edge.Visible = true;
                pip.Edge.Scale = Vector2.One * (d / (104f * Mathf.Max(0.05f, pip.Root.Scale.X)));
                pip.Edge.Modulate = new Color(ringColor, 0.75f * f);
            }
            else
            {
                pip.Edge.Visible = false;
            }
        }

        if (pip.Plus != null)
        {
            // 原版行为：加号挂在这颗球上，呼吸亮着（α 0.8~1.0、尺寸 0.94~1.10）；
            // 获得窗口结束时只做透明度淡出，位置与尺寸都不动。
            bool showPlus = pip.Incoming || pip.PlusT > 0.01f;
            pip.Plus.Visible = showPlus;
            if (showPlus)
            {
                float orbPx = 46f * size;
                float breath = 0.5f - 0.5f * Mathf.Cos(time * 3.4f);
                float fade = pip.Incoming ? 1f : pip.PlusT * pip.PlusT * (3f - 2f * pip.PlusT);
                float plusParent = Mathf.Max(0.05f, pip.Root.Scale.X);
                float baseScale = orbPx / 46f; // 贴图 32px、十字 24px：1:1 对应 46px 球
                pip.Plus.Modulate = new Color(1f, 1f, 1f, (0.8f + 0.2f * breath) * fade);
                pip.Plus.Scale = Vector2.One * baseScale * (0.94f + 0.16f * breath) / plusParent;
                pip.Plus.Position = new Vector2(11.04f, -10.12f) * baseScale / plusParent;
            }
        }

        if (burst > 0f)
        {
            tint = tint.Lerp(accent, burst * 0.85f);
        }

        // 常态不发光：只有出现/刷新、消耗、回能、预测这几种「闪」才有光晕
        float lackPulse = pip.Lacking ? 0.5f + 0.5f * Mathf.Sin(time * 6.5f) : 0f;
        float glowAlpha = Mathf.Min(1f,
            (pip.WillSpend ? (0.10f + 0.064f * pulse + 0.12f * (0.5f + 0.5f * Mathf.Sin(time * 2.0f))) * pk : 0f)
            + (pip.Lacking ? (0.26f + 0.18f * lackPulse) * pk : 0f)
            + gain * 0.85f * (pip.Incoming ? 0f : 1f) + burst * 0.45f
            ) * alpha;
        pip.Glow.Visible = glowAlpha > 0.01f;
        pip.Glow.Modulate = new Color(pip.Lacking ? lackRed : tint, glowAlpha);
        // 光晕半径：原版就是 0.6828 × 1.6
        pip.Glow.Scale = Vector2.One * (0.6828125f
                                        * (0.55f + 0.06f * pulse + 0.15f * burst + 0.05f * gain));



        float tintV = Mathf.Lerp(1f, 0.8f, shell);
        if (!pip.Lacking)
        {
            tintV = Mathf.Lerp(tintV, 1.3f, pk);
        }

        Color iconTint = new Color(tintV, tintV, tintV, 1f);
        if (pip.Lacking)
        {
            iconTint = new Color(0.55f, 0.55f, 0.58f, 1f); // 付不起：球变灰
        }
        if (gain > 0f)
        {
            iconTint = iconTint.Lerp(new Color(GainGlow.R, GainGlow.G, GainGlow.B, 1f), gain * 0.45f);
        }

        if (pip.Incoming && gain <= 0.01f)
        {
            iconTint = new Color(0.45f, 0.85f, 0.5f, 1f); // ghost = 绿
        }


        float iconAlpha = alpha * Mathf.Lerp(1f, 0.55f, shell)
                          * (pip.Incoming && gain <= 0.01f ? 0.62f : 1f);
        float grey = pip.Lacking ? 0.9f : (pip.WillSpend ? 0f : 0.9f * shell);
        float white = (pip.Lacking ? 0f : 0.05f * pk) + 0.3f * gain + 0.15f * burst
                      + (pip.Incoming ? 0.15f : 0f);

        // 刷新瞬间：图案内部一次性白闪（白混只作用于图标像素，不外扩）
        if (pip.WhiteHit > 0.001f)
        {
            white = Mathf.Max(white, 0.92f * pip.WhiteHit);
        }

        pip.Icon.Modulate = new Color(iconTint.R, iconTint.G, iconTint.B, iconAlpha);
        pip.Tone.SetShaderParameter(P_Grey, grey);
        pip.Tone.SetShaderParameter(P_White, white);
        pip.Tone.SetShaderParameter(P_Tint, new Vector3(1f, 1f, 1f));

        if (ghost)
        {
            // 原版 Kind.Incoming 的着色：去饱和 0.85、绿色 tint(0.8,1.5,0.8)、无白混
            float idleGhost = 0.5f - 0.5f * Mathf.Cos(time * 3.4f);
            float shade = 1f + 0.15f * idleGhost;
            float ghostAlpha = alpha * (0.30f + 0.12f * idleGhost);
            pip.Icon.Modulate = new Color(shade, shade, shade, ghostAlpha);
            pip.Tone.SetShaderParameter(P_Grey, 0.85f);
            pip.Tone.SetShaderParameter(P_Tint, new Vector3(0.8f, 1.5f, 0.8f));
            pip.Tone.SetShaderParameter(P_White, 0f);
        }

        // 扫描带经过时：只轻微提亮 + 边缘高光，不做整颗闪光
        // G2：硬光斜扫（每 1.4 s 一次，0.35 s 扫完）
        if (pip.Sheen != null)
        {
            pip.Sheen.Visible = pk > 0.02f;
            if (pip.Sheen.Visible)
            {
                float cyc = Mathf.PosMod(time * 1.4f + pip.Seed * 0.37f, 1.4f) / 1.4f;
                var mat = (ShaderMaterial)pip.Sheen.Material;
                mat.SetShaderParameter(P_Phase, -0.35f + cyc * 1.75f);
                mat.SetShaderParameter(P_Strength, 1.15f * pk);
                mat.SetShaderParameter(P_Tint, new Vector3(1f, 1f, 1f));
            }
        }

        float shine = 0.2f * pk + 0.4f * burst;
        shine *= alpha;
        pip.Shine.Visible = shine > 0.01f;
        if (pip.Shine.Visible)
        {
            var shineMat = (ShaderMaterial)pip.Shine.Material;
            shineMat.SetShaderParameter(P_Strength, shine);
            shineMat.SetShaderParameter(P_Tint, new Vector3(accent.R, accent.G, accent.B));
        }
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
