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

[HarmonyPatch(typeof(NEnergyCounter), "_Ready")]
internal static class EnergyCounterReadyPatch
{
    private static readonly AccessTools.FieldRef<NEnergyCounter, Player> PlayerOf =
        AccessTools.FieldRefAccess<NEnergyCounter, Player>("_player");

    private static void Postfix(NEnergyCounter __instance)
    {
        try
        {
            Player player = PlayerOf(__instance);
            if (player == null)
            {
                return;
            }

            Texture2D texture = OrbRenderer.FirstSprite(__instance);
            Callable.From(() => OrbRenderer.Tick(player, __instance, texture, 0.016f)).CallDeferred();
        }
        catch (Exception e)
        {
            GD.PushWarning($"[BangDreamInSpire/handpips] counter ready: {e.Message}");
        }
    }
}

[HarmonyPatch(typeof(NEnergyCounter), "_Process")]
internal static class EnergyCounterProcessPatch
{
    private static readonly AccessTools.FieldRef<NEnergyCounter, Player> PlayerOf =
        AccessTools.FieldRefAccess<NEnergyCounter, Player>("_player");

    private static void Postfix(NEnergyCounter __instance)
    {
        try
        {
            Player player = PlayerOf(__instance);
            if (player == null)
            {
                return;
            }

            float delta = Mathf.Clamp((float)__instance.GetProcessDeltaTime(), 0.001f, 0.1f);
            OrbRenderer.Tick(player, __instance, null, delta);

        }
        catch (Exception e)
        {
            if (Config.Data.Debug)
            {
                GD.PushWarning($"[BangDreamInSpire/handpips] tick: {e.Message}");
            }
        }
    }
}
