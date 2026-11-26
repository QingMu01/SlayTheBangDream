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

/// <summary>
/// 只剩类壳：原 OrbProbe.cs（约 1250 行）按职责拆到同目录的 partial 文件，成员内部一行未改。
///   OrbWatchdog.cs    CombatWatchdog（战斗外看门狗）
///   OrbSecondary.cs   UpdateSecondaries / SampleFromScreen（副资源）
///   OrbPredict.cs     UpdateIncoming / SetIncoming / ClearIncoming / PreviewCost / DescriptionOf /
///                     CountIcons / ResourceToken / IsLingeredCharacter / IsRegent / CurrentCardHolder /
///                     HeadAwareCardsTop / HittableEnemies
///   OrbOverlay.cs     OverlayFor / Hide / 图标解析 / LoadTexture / 颜色采样（DominantColor 及其取图链）
///   OrbDiagnostics.cs Debug 日志（位移探针 + fx）
/// 新成员按职责进上面的文件，不要再往这个壳里加。
/// </summary>
internal static partial class OrbRenderer
{
}
