using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using BangDreamInSpire.Scripts.Mechanics.Perform;

namespace BangDreamInSpire.Scripts.Nodes.HandPips;

/// <summary>
/// 预知对账探针：记录余音溢出时<b>实际</b>送进歌单（或插曲附魔牌进手牌）的那张牌，
/// 用来核对 <see cref="LingeredPrecognition"/> 预告的是不是同一张。只在 <c>Config.Data.Debug</c> 下打印。
/// </summary>
[HarmonyPatch(typeof(InsertSongRules), nameof(InsertSongRules.MoveIntoPerformArea))]
internal static class PrecogMoveProbe
{
    private static void Prefix(CardModel card)
    {
        if (!Config.Data.Debug)
        {
            return;
        }

        GD.Print($"[BangDreamInSpire/handpips] precog ACTUAL draw: {card?.Id.Entry}");
    }
}
