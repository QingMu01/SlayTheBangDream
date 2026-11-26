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
    private const int MaxPips = 40;
    private const float BaseSpacing = 50f;
    private const float SubRowScale = 0.85f;
    private const string StarIconPath = "res://images/ui/combat/energy_star.png";

    private static readonly Color FallbackGlow = new(1f, 0.82f, 0.45f);
    private static readonly Color GainGlow = new(0.4f, 1f, 0.45f);
    private static readonly Color PlusGreen = new(0.45f, 1f, 0.5f);

    private sealed class Pip
    {
        public Node2D Root;
        public Sprite2D Icon;
        public Sprite2D Glow;
        public Sprite2D Shine;
        public Sprite2D Plus;
        public Sprite2D Ring;
        public Sprite2D Gem;
        public Sprite2D Edge;
        public Sprite2D DashRing;
        public Sprite2D EchoMid;
        public float Flash;
        public Sprite2D Sheen;
        public float RippleT = 99f;
        public ShaderMaterial Tone;
        public float Appear;
        public float Lit = 1f;
        public float Gain;
        public float Burst;
        public bool LitTarget = true;
        public bool WillSpend;
        public bool Lacking;
        public bool Incoming;
        public float PreviewK;
        public float WhiteHit;
        public float PlusT;
        public bool Initialized;
        public int Seed;

        // 静止球快路径：Still = 连续处于「完全静止」的帧数（>=2 才真的跳过绘制），
        // BakedSize = 上一帧完整绘制时用的球径，尺寸一变就必须重绘一次。
        public int Still;
        public float BakedSize;
    }

    /// <summary>一排球的几何：换行后的列数、行高、整块宽高。</summary>
    private sealed class Block
    {
        public int Total;
        public int Cols = 1;
        public int Rows;
        public float Spacing = BaseSpacing;
        public float Size = 1f;
        public float Step;
        public float Height;
        public float Width;
    }

    private sealed class Row
    {
        public Node2D Root;
        public readonly List<Pip> Pips = new();
        public Texture2D Texture;
        public Color Glow = FallbackGlow;
        public bool NeedsSample;
        public int SampleTries;
        public double SampleNextAt;
        public string Key = string.Empty;
        public string IconPath = string.Empty;
        public int Incoming;
        public int PendingIncoming;
        public float IncomingHold;
        public int LastLoggedIncoming = -1;
        public int Amount;
        public int DisplayTotal;
        public int PreviewCostCache;
        public int PendingGain;
        public int PendingPlus;
        public float LastWidth;
        public float LastOrbPx = 46f * 0.81f;
        public bool SparkRequest;
        public int RippleFrom = -1;
        public int ConsumeFrom = -1;
        public int ConsumeCount;
        public int CashOutPending;
        public readonly List<Node2D> BurstFx = new();
        public int GainStart = -1;
        public float YOffset;
        public float XOffset;
        public float Fan;

        public readonly List<Spark> Sparks = new();
        public bool IsSecondary;
    }

    private sealed class Spark
    {
        public Node2D Root;
        public Sprite2D Sprite;
        public Line2D Line;
        public Spark A;
        public Spark B;
        public float Angle;
        public float Radius;
        public float Size;
        public float Delay;
        public Vector2 Anchor;
        public float Life;
        public bool IsLine;
        public float Spin;
        public float Rotate;
    }

    private sealed class Overlay
    {
        public Node2D Root;
        public Node2D GripRoot;
        public Line2D GripTop;
        public Line2D GripBottom;
        public Line2D GripLeft;
        public Line2D GripRight;
        public Polygon2D[] GripHandles = new Polygon2D[4];
        public int GripActive = -1;
        public bool GripLatch;
        public float GripHoverT;
        public double SecCostAt;
        public double ProbeAt;
        public float ProbeLastRootY = float.NaN;
        public float ProbeLastPipY = float.NaN;
        public float ProbeMaxRootDY;
        public float ProbeMaxPipDY;
        public Vector2 GripAnchorScreen;
        public float GripStartScale = 1f;
        public float GripStartDist = 1f;
        public Row Energy = new();
        public readonly Dictionary<string, Row> Secondary = new();
        public float Time;
        public bool Placed;
        public bool FreeSeeded;

        // 自由摆放：进入时取一次的父链累积缩放（用来抵消父缩放，开关一开大小不变）
        public float FreeParentScale = 1f;
        public bool HaveLast;
        public float LastCardsTop;
        public float LastFocusX;
        public bool Dragging;
        public bool DragLatch;
        public Vector2 GrabOffset;
        public double CheckedAt;
        public bool WasHidden;
        public int LastEnergy = -1;
        public int LastTurn = -1;
        public int LastHolderCount = -1;
        public bool HiddenAfterPlay;
        public float Opacity = 1f;
        public float Dim = 1f;
        public float? RowX;
        public float? RowY;
        public float? HoldY;
        public float NoHandT;
        public double SettleAt;
        public int RawEnergy = -1;
        public bool HideScheduled;
        public double HideStart;
        public List<SecondaryEntry> Entries = new();
        public readonly List<SecondaryEntry> EntriesBuf = new();
        public readonly HashSet<string> LoggedSecondary = new();

        // 同帧去重：Tick 可能在同一渲染帧被调两次（_Ready 的 CallDeferred 一次 + _Process 一次），
        // 重复跑会让 overlay.Time 双倍推进、整排动画双倍速。记下最后处理的帧号，同帧再进直接返回。
        public ulong LastTickFrame;
    }

    public readonly struct SecondaryEntry
    {
        public readonly string Key;
        public readonly string IconPath;
        public readonly int Amount;

        public SecondaryEntry(string key, string iconPath, int amount)
        {
            Key = key;
            IconPath = iconPath;
            Amount = amount;
        }
    }

    private static readonly Dictionary<Player, Overlay> Overlays = new();
    private static readonly Dictionary<ulong, Color> DominantCache = new();
    private static readonly HashSet<Player> Logged = new();

    private static bool _tickLogged;

    private static double _fxLogAt;
    private static double _ringLog;

    private static bool _layoutLogged;
    private static bool _snap;
}
