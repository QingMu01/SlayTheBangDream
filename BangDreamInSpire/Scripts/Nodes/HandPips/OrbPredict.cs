using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace BangDreamInSpire.Scripts.Nodes.HandPips;

internal static partial class OrbRenderer
{
    private static void ClearIncoming(Overlay overlay)
    {
        SetIncoming(overlay.Energy, 0);
        foreach (Row row in overlay.Secondary.Values)
        {
            SetIncoming(row, 0);
        }

        _predHolder = null;
    }

    private static float HeadAwareCardsTop(NPlayerHand hand, IReadOnlyList<NHandCardHolder> holders,
        float cardsTop, float focusX)
    {
        foreach (NHandCardHolder holder in holders)
        {
            if (holder == null || !GodotObject.IsInstanceValid(holder) || !holder.IsVisibleInTree())
            {
                continue;
            }

            try
            {
                TextureRect head = FindHeadCached(holder);
                if (head == null || !GodotObject.IsInstanceValid(head) || !head.IsVisibleInTree())
                {
                    continue;
                }

                Rect2 r = head.GetGlobalRect();
                if (Mathf.Abs(r.GetCenter().X - focusX) < 170f && r.Position.Y > 0f && r.Position.Y < cardsTop)
                {
                    cardsTop = r.Position.Y;
                }
            }
            catch
            {
                // 忽略
            }
        }

        return cardsTop;
    }

    internal static bool IsLingeredCharacter(Player player)
    {
        try
        {
            string assembly = player?.Character?.GetType().Assembly.GetName().Name ?? string.Empty;
            string type = player?.Character?.GetType().Name ?? string.Empty;
            return assembly is "ItsCrychic" or "BangDreamInSpire" or "MutsumiNoDeck"
                   || type.Contains("Sakiko", StringComparison.OrdinalIgnoreCase)
                   || type.Contains("Mutsumi", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsRegent(Player player)
    {
        try
        {
            return player.Character != null
                   && player.Character.GetType().Name.Contains("Regent", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static readonly AccessTools.FieldRef<NPlayerHand, NCardPlay> CurrentPlayField =
        AccessTools.FieldRefAccess<NPlayerHand, NCardPlay>("_currentCardPlay");

    private static readonly HashSet<string> _loggedCards = new();
    private static readonly HashSet<string> _loggedDescs = new();

    private static NHandCardHolder _predHolder;
    private static double _predAt;
    private static double _hoverLostAt = -1;

    private static NHandCardHolder CurrentCardHolder(NPlayerHand hand)
    {
        if (hand == null)
        {
            return null;
        }

        if (hand.InCardPlay)
        {
            try
            {
                NCardPlay play = CurrentPlayField(hand);
                if (play != null && GodotObject.IsInstanceValid(play) && GodotObject.IsInstanceValid(play.Holder))
                {
                    return play.Holder;
                }
            }
            catch
            {
                // 退回悬停
            }
        }

        return hand.FocusedHolder;
    }

    private static void UpdateIncoming(Overlay overlay, NHandCardHolder holder, Player player)
    {
        // holder 由调用方（Tick）在帧首取一次传进来，这里不再反射 CurrentCardHolder，
        // 保证同一帧的 previewCost / ghost / 副资源预扣价看的是同一张牌。

        // 没在悬停/拖动（牌打出去了）：立刻清掉绿色预测和加号，跟能量一起消失
        if (holder == null)
        {
            // 卡面抬起/战斗抖动时 FocusedHolder 会连续几帧读不到：累计 0.12 s 才算真的没悬停。
            // 这是 incoming 0/1 抖动的头号来源。
            double nowNull = Time.GetTicksMsec() / 1000.0;
            if (_hoverLostAt < 0)
            {
                _hoverLostAt = nowNull;
            }

            if (nowNull - _hoverLostAt < 0.12)
            {
                return;
            }

            SetIncoming(overlay.Energy, 0);
            foreach (Row r in overlay.Secondary.Values)
            {
                SetIncoming(r, 0);
            }

            _predHolder = null;
            return;
        }

        _hoverLostAt = -1;

        // 只在换牌/拖牌目标变化时重算，避免每帧读卡牌数据（也避免干扰卡面动画）
        double now = Time.GetTicksMsec() / 1000.0;
        if (holder == _predHolder && now - _predAt < 0.25)
        {
            return;
        }

        _predHolder = holder;
        _predAt = now;

        MegaCrit.Sts2.Core.Models.CardModel card = holder?.CardModel;

        var values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (card?.DynamicVars != null)
        {
            try
            {
                foreach (KeyValuePair<string, MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar> pair in card.DynamicVars)
                {
                    values[pair.Key ?? string.Empty] = pair.Value?.IntValue ?? 0;
                }

                if (Config.Data.Debug)
                {
                    string id = card.Id?.ToString() ?? card.GetType().Name;
                    if (_loggedCards.Add(id))
                    {
                        GD.Print("[BangDreamInSpire/handpips] vars " + id + ": " +
                                 string.Join(", ", System.Linq.Enumerable.Select(values, kv => kv.Key + "=" + kv.Value)));
                    }
                }
            }
            catch
            {
                // 变量枚举失败就当没有预测
            }
        }

        // 延迟到「下回合」的收益不预测（原版 CardFlow 也只算本手立即结算的）
        bool delayed = false;
        string desc = DescriptionOf(card);
        if (!string.IsNullOrEmpty(desc)
            && (desc.Contains("下回合") || desc.Contains("回合开始")))
        {
            delayed = true;
        }

        // 主能量预测：卡带 Energy 变量 + 描述里是「获得」才算（费用类/X 费不会带这个变量）
        int energyPred = 0;
        foreach (KeyValuePair<string, int> kv in values)
        {
            if (kv.Key.Equals("Energy", StringComparison.OrdinalIgnoreCase) && kv.Value > 0 && kv.Value < 20)
            {
                energyPred = kv.Value;
            }
        }

        if (energyPred > 0)
        {
            string d = DescriptionOf(card);
            bool gains = !string.IsNullOrEmpty(d) && (d.Contains("获得") || d.Contains("gain", StringComparison.OrdinalIgnoreCase));
            if (!gains)
            {
                energyPred = 0;
            }
        }

        SetIncoming(overlay.Energy, delayed ? 0 : energyPred);

        foreach (Row row in overlay.Secondary.Values)
        {
            string token = ResourceToken(row.Key, row.IconPath);
            int pred = 0;
            if (!string.IsNullOrEmpty(token))
            {
                foreach (KeyValuePair<string, int> kv in values)
                {
                    if (kv.Key.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        pred = Math.Max(pred, kv.Value);
                    }
                }
            }

            // 祥子的角色规则：余音 = 这张牌的费用（3 费 +3，X 费按 X 不计）+ 特殊效果给的额外余音；
            // 带「休止」的牌不吃费用那一份。
            // 群伤 + 每次命中给余音：按「生成 X 颗 × 可命中敌人数 + 获得 Y 颗」算
            if (token.Equals("lingered", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(desc))
            {
                int gen = CountIconsInSegment(desc, "生成", "。", row.IconPath);
                int gain = CountIconsInSegment(desc, "获得", "。", row.IconPath);
                if (Config.Data.Debug && !string.IsNullOrEmpty(desc) && _loggedDescs.Add(desc))
                {
                    int t = desc.Contains("所有敌人") ? HittableEnemies(player) : 1;
                    GD.Print($"[BangDreamInSpire/handpips] pred: gen={gen} gain={gain} targets={t} desc={desc.Replace("\n", " | ")}");
                }

                if (gen > 0 || gain > 0)
                {
                    int targets = desc.Contains("所有敌人") ? HittableEnemies(player) : 1;
                    pred = Math.Max(pred, gen * targets + gain);
                }
            }

            if (token.Equals("lingered", StringComparison.OrdinalIgnoreCase) && IsLingeredCharacter(player))
            {
                int cost = 0;
                bool costsX = card?.EnergyCost != null && card.EnergyCost.CostsX;
                if (costsX)
                {
                    // X 费：按"花光当前全部能量"推，生成量 = 这份实耗（原版口径）
                    try
                    {
                        cost = Math.Max(0, player?.PlayerCombatState?.Energy ?? 0);
                    }
                    catch
                    {
                        cost = 0;
                    }
                }
                else if (card?.EnergyCost != null)
                {
                    try
                    {
                        cost = Math.Max(0, card.EnergyCost.GetWithModifiers(CostModifiers.All));
                    }
                    catch
                    {
                        cost = 0;
                    }
                }

                bool rest = !string.IsNullOrEmpty(desc) && desc.Contains("休止");
                pred += rest ? 0 : cost;
            }

            SetIncoming(row, delayed ? 0 : pred);
        }
    }

    private static readonly Dictionary<string, string> _descCache = new();

    /// <summary>卡面描述按卡实例缓存：只在换卡时读一次，避免每帧刷新把卡牌动画打断。</summary>
    private static string DescriptionOf(MegaCrit.Sts2.Core.Models.CardModel card)
    {
        if (card == null)
        {
            return string.Empty;
        }

        string key = (card.Id?.ToString() ?? card.GetType().Name) + (card.IsUpgraded ? "+" : string.Empty);
        if (_descCache.TryGetValue(key, out string cached))
        {
            return cached;
        }

        string text = string.Empty;
        try
        {
            text = card.GetDescriptionForPile(PileType.Hand) ?? string.Empty;
        }
        catch
        {
            text = string.Empty;
        }

        _descCache[key] = text;
        return text;
    }


    /// <summary>从资源 id / 图标文件名里取一个可用于匹配变量名的词（如 lingered）。</summary>
    private static string ResourceToken(string key, string iconPath)
    {
        string source = !string.IsNullOrEmpty(iconPath) ? iconPath : key ?? string.Empty;
        if (string.IsNullOrEmpty(source))
        {
            return string.Empty;
        }

        int slash = source.LastIndexOf('/');
        string name = slash >= 0 ? source.Substring(slash + 1) : source;
        int dot = name.LastIndexOf('.');
        if (dot > 0)
        {
            name = name.Substring(0, dot);
        }

        string[] parts = name.Split(new[] { '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            if (parts[i].Length >= 4 && !parts[i].Equals("small", StringComparison.OrdinalIgnoreCase))
            {
                return parts[i];
            }
        }

        return string.Empty;
    }

    private static void SetIncoming(Row row, int value)
    {
        // 只登记期望值；真正切换在 PaintRow 里做 50 ms 防抖。
        // 悬停/焦点在相邻帧反复判定时，ghost 才不会一帧有一帧没、在末尾闪来闪去。
        row.PendingIncoming = Math.Clamp(value, 0, MaxPips);
        if (row.PendingIncoming == 0)
        {
            // 只取消"还没生成"的加号请求；已经弹出的那些让它们自己淡出（1.0 s），
            // 这里直接 QueueFree 会表现为"+ 号啪地消失"。
            row.PendingPlus = 0;
        }
    }

    private static int CountIconsInSegment(string desc, string startWord, string endWord, string iconPath)
    {
        if (string.IsNullOrEmpty(desc) || string.IsNullOrEmpty(iconPath))
        {
            return 0;
        }

        int s = desc.IndexOf(startWord, StringComparison.Ordinal);
        if (s < 0) return 0;
        int e = desc.IndexOf(endWord, s, StringComparison.Ordinal);
        if (e < 0) e = desc.Length;
        string seg = desc.Substring(s, e - s);
        return CountIcons(seg, iconPath);
    }

    private static int HittableEnemies(Player player)
    {
        try
        {
            var state = player?.Creature?.CombatState;
            return state?.HittableEnemies?.Count ?? 1;
        }
        catch
        {
            return 1;
        }
    }

    private static int CountIcons(string desc, string path)
    {
        if (string.IsNullOrEmpty(desc) || string.IsNullOrEmpty(path))
        {
            return 0;
        }

        string token = "[img]" + path + "[/img]";
        int count = 0;
        int index = 0;
        while (true)
        {
            index = desc.IndexOf(token, index, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                break;
            }

            count++;
            index += token.Length;
        }

        return count;
    }

    private static int PreviewCost(Player player, NHandCardHolder focused)
    {
        try
        {
            // focused 由 Tick 在帧首统一取一次（拖动中的那张优先，其次悬停），
            // 这里不再重复反射 CurrentCardHolder，避免同帧读到不同的牌。
            if (focused == null || !GodotObject.IsInstanceValid(focused) || !focused.IsVisibleInTree())
            {
                return 0;
            }

            CardModel card = focused.CardModel;
            if (card == null || card.EnergyCost == null)
            {
                return 0;
            }

            // X 费牌：看作「花掉当前全部能量」
            if (card.EnergyCost.CostsX)
            {
                int current = 0;
                try
                {
                    current = Math.Max(0, player?.PlayerCombatState?.Energy ?? 0);
                }
                catch
                {
                    current = 0;
                }

                return Math.Clamp(current, 0, MaxPips);
            }

            return Math.Max(0, card.EnergyCost.GetWithModifiers(CostModifiers.All));
        }
        catch
        {
            return 0;
        }
    }
}
