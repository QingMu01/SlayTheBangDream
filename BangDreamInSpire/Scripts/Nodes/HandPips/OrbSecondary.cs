using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;

namespace BangDreamInSpire.Scripts.Nodes.HandPips;

internal static partial class OrbRenderer
{
    private static void UpdateSecondaries(Overlay overlay, Player player, PlayerCombatState state)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        if (now - overlay.CheckedAt > 0.25)
        {
            overlay.CheckedAt = now;
            // 复用缓冲：每 0.25 s 一次也不 new List（EntriesBuf 全局只此一份，单线程 UI 调用）
            var list = overlay.EntriesBuf;
            list.Clear();

            if (Config.Data.Mode == OrbDisplayMode.All || Config.Data.Mode == OrbDisplayMode.SecondaryOnly)
            {
                if (SecondaryBridge.Available)
                {
                    SecondaryBridge.Snapshot(player, list);
                }
                else if (state.Stars > 0 || IsRegent(player))
                {
                    list.Add(new SecondaryEntry("stars", StarIconPath, state.Stars));
                }
            }

            overlay.Entries = list;
        }

        var seen = SeenBuf;
        seen.Clear();
        foreach (SecondaryEntry entry in overlay.Entries)
        {
            seen.Add(entry.Key);
            if (!overlay.Secondary.TryGetValue(entry.Key, out Row row))
            {
                row = new Row
                {
                    Root = new Node2D { Name = "Row_" + entry.Key },
                    Key = entry.Key,
                    IsSecondary = true,
                };
                overlay.Root.AddChild(row.Root);
                overlay.Secondary[entry.Key] = row;

            }

            if (Config.Data.Debug && !overlay.LoggedSecondary.Contains(entry.Key))
            {
                overlay.LoggedSecondary.Add(entry.Key);
                GD.Print($"[BangDreamInSpire/handpips] secondary '{entry.Key}' icon='{entry.IconPath}' amount={entry.Amount}");
            }

            if (row.Texture == null || row.Texture.ResourcePath != entry.IconPath)
            {
                Texture2D tex = LoadTexture(entry.IconPath);
                if (Config.Data.Debug)
                {
                    GD.Print($"[BangDreamInSpire/handpips] secondary tex '{entry.IconPath}' loaded={tex != null} size=" +
                             $"{(tex != null ? tex.GetWidth() + "x" + tex.GetHeight() : "-")}");
                }

                if (tex != null)
                {
                    row.Texture = tex;
                    row.IconPath = entry.IconPath;
                    row.Glow = DominantColor(tex, FallbackGlow);
                    foreach (Pip pip in row.Pips)
                    {
                        pip.Icon.Texture = tex;
                        pip.Shine.Texture = tex;
                        var s = Vector2.One * (46f / Math.Max(1, Math.Max(tex.GetWidth(), tex.GetHeight())));
                        pip.Icon.Scale = s;
                        pip.Shine.Scale = s;
                    }
                }
            }

            if (entry.Amount != row.Amount && Config.Data.Debug)
            {
                GD.Print($"[BangDreamInSpire/handpips] lingered {row.Amount}->{entry.Amount}");
            }

            if (entry.Amount > row.Amount)
            {
                int delta = entry.Amount - row.Amount;
                row.PendingGain += delta;
                row.SparkRequest = true;
                row.RippleFrom = row.Amount; // 只有新得到的几颗播波纹

                row.GainStart = row.Amount; // 只有新得到的那几颗播刷新动画
                if (Config.Data.GainEffects)
                {
                    row.PendingPlus += Math.Min(2, delta);
                }
            }
            else if (entry.Amount < row.Amount)
            {
                // 花掉副资源：不留暗格，直接从消耗位置爆散
                int lost = row.Amount - entry.Amount;
                row.ConsumeFrom = entry.Amount;
                row.ConsumeCount = lost;
                // 余音上限就是 7：攒满的那一瞬游戏会立刻 Lose(7)，
                // 0.25 s 的采样经常只看到 6->0（看不到 7），所以两种都要认。
                bool cashOut = lost >= 7 || (row.Amount >= 6 && entry.Amount <= 1);
                if (cashOut)
                {
                    int times = Mathf.Clamp(Mathf.Max(lost, 7) / 7, 1, 3);
                    row.CashOutPending = times;
                    if (Config.Data.Debug)
                    {
                        GD.Print($"[BangDreamInSpire/handpips] cashout x{times} ({row.Amount}->{entry.Amount})");
                    }
                }

                if (Config.Data.Debug)
                {
                    GD.Print($"[BangDreamInSpire/handpips] lingered {row.Amount}->{entry.Amount}");
                }

            }

            bool amountIncreased = entry.Amount > row.Amount;
            bool amountChanged = row.Amount != entry.Amount;
            row.Amount = entry.Amount;
            // 固定格数：未点亮的部分画成暗球（和主能量的暗格一致），默认 6 格；
            // 只是"暗格补位下限"，不截断：余音超过 6 照样全画，预测 ghost 也不受这个数限制。
            row.DisplayTotal = Mathf.Max(entry.Amount, Mathf.Clamp(Config.Data.SecondarySlots, 0, 12));

            if (amountChanged && amountIncreased)
            {
                // 只有真的到账才清预测；数量减少（资源被花掉）与预测无关，
                // 在那里清一次会让 PendingIncoming 卡在 0 直到下次 0.25s 重算，ghost 反复退场。
                row.Incoming = 0;
                row.PendingIncoming = 0;
                row.IncomingHold = 0f;
            }
        }

        RemoveBuf.Clear();
        RemoveBuf.AddRange(overlay.Secondary);
        foreach (var pair in RemoveBuf)
        {
            if (seen.Contains(pair.Key))
            {
                continue;
            }

            if (GodotObject.IsInstanceValid(pair.Value.Root))
            {
                pair.Value.Root.QueueFree();
            }

            overlay.Secondary.Remove(pair.Key);
        }
    }

    /// <summary>
    /// 图集纹理取不到像素时（.tres 里是 AtlasTexture），直接从已渲染画面里取这颗球的颜色：
    /// 只统计饱和度够的像素，避开背景。
    /// </summary>
    private static void SampleFromScreen(Row row)
    {
        try
        {
            // 全屏 GetImage() 是同步管线 stall：最多 2 次/秒，连续 4 次拿不到就放弃（保留 FallbackGlow）
            double nowSample = Time.GetTicksMsec() / 1000.0;
            if (nowSample < row.SampleNextAt)
            {
                return;
            }

            row.SampleNextAt = nowSample + 0.5;
            if (++row.SampleTries > 4)
            {
                row.NeedsSample = false;
                return;
            }

            Pip target = null;
            foreach (Pip pip in row.Pips)
            {
                if (pip.Root.Visible && pip.Lit > 0.5f)
                {
                    target = pip;
                    break;
                }
            }

            if (target == null)
            {
                return;
            }

            Viewport viewport = row.Root.GetViewport();
            Image image = viewport?.GetTexture()?.GetImage();
            if (image == null)
            {
                return;
            }

            if (image.IsCompressed())
            {
                image.Decompress();
            }

            Vector2 center = target.Root.GlobalPosition;
            float half = 16f;
            int x0 = Mathf.Clamp((int)(center.X - half), 0, image.GetWidth() - 1);
            int x1 = Mathf.Clamp((int)(center.X + half), 0, image.GetWidth() - 1);
            int y0 = Mathf.Clamp((int)(center.Y - half), 0, image.GetHeight() - 1);
            int y1 = Mathf.Clamp((int)(center.Y + half), 0, image.GetHeight() - 1);

            double r = 0, g = 0, b = 0, total = 0;
            for (int y = y0; y <= y1; y += 2)
            {
                for (int x = x0; x <= x1; x += 2)
                {
                    Color c = image.GetPixel(x, y);
                    float max = Mathf.Max(c.R, Mathf.Max(c.G, c.B));
                    float min = Mathf.Min(c.R, Mathf.Min(c.G, c.B));
                    if (max < 0.25f || max - min < 0.08f)
                    {
                        continue;
                    }

                    double weight = (max - min) * max;
                    r += c.R * weight;
                    g += c.G * weight;
                    b += c.B * weight;
                    total += weight;
                }
            }

            if (total < 0.02)
            {
                return;
            }

            var avg = new Color((float)(r / total), (float)(g / total), (float)(b / total));
            float peak = Mathf.Max(avg.R, Mathf.Max(avg.G, avg.B));
            if (peak > 0.01f)
            {
                avg = new Color(avg.R / peak, avg.G / peak, avg.B / peak);
            }

            row.Glow = avg.Lerp(Colors.White, 0.1f);
            row.NeedsSample = false;
            if (Config.Data.Debug)
            {
                GD.Print($"[BangDreamInSpire/handpips] sampled glow from screen: ({row.Glow.R:F2},{row.Glow.G:F2},{row.Glow.B:F2})");
            }
        }
        catch (Exception e)
        {
            row.NeedsSample = false;
            if (Config.Data.Debug)
            {
                GD.PushWarning($"[BangDreamInSpire/handpips] sample: {e.Message}");
            }
        }
    }
}