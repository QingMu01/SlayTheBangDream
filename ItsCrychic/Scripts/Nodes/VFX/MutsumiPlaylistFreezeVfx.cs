using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Interfaces.Mechanics;
using BangDreamInSpire.Scripts.Utils.Infos;
using BangDreamInSpire.Scripts.Utils;
using Godot;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Helpers;

namespace ItsCrychic.Scripts.Nodes.VFX;

/// <summary>歌单冻结提线效果。每列共用外侧提线以避开行与角色，行旋转由
/// NPerformArea 负责。几何统一使用 ItemContainer 局部坐标与斜切四边形，
/// 提线层级低于分组激活角标。</summary>
[Tool]
public partial class MutsumiPlaylistFreezeVfx : Node2D, IPerformAreaFreezeVisual
{
    public const string NodeName = "PlaylistFreezeVfx";

    public Node2D View => this;

    private const float FadeInDuration = 0.25f;
    private const float FadeOutDuration = 0.30f;

    private const float WireAlpha = 0.55f;
    private const float TieAlpha = 0.45f;
    private const float KnotAlpha = 0.60f;
    private const float RimAlpha = 0.12f;
    private const float FilmAlpha = 0.06f;
    private const float WireWidth = 1.6f;
    private const float TieWidth = 1.2f;
    private const float KnotSize = 3.6f;
    private const float BreathPeriod = 1.8f;

    /// <summary>提线上端的渐隐段。</summary>
    private const float WireFadeAbove = 160f;
    private const float WireFadeBelow = 14f;

    /// <summary>提线与列外缘的间距，需避开标题色带。</summary>
    private const float WireOffset = 18f;

    private const float TieInset = 3.5f;

    private const float TieDrop = 11f;

    /// <summary>下滑高光的长度与周期。</summary>
    private const float ShimmerLength = 26f;
    private const float ShimmerPeriod = 2.6f;

    private static readonly Color WireColor = new("#ded8f6");
    private static readonly Color TieColor = new("#f2eeff");
    private static readonly Color KnotColor = new("#efeaff");
    private static readonly Color FilmColor = new("#b9a6ff");

    /// <summary>暗色描边保证浅色背景上的轮廓可读。</summary>
    private static readonly Color CordShadowColor = new("#0a0812");

    private PerformRowGroup[] _groups = [];
    private float _fade;
    private float _time;
    private Tween? _fadeTween;
    private bool _previewRows;
    private bool _hiding;
    private int _previewGroupRows = 3;

    [Export]
    public bool Play
    {
        get => false;
        set
        {
            if (value)
                Replay();
        }
    }

    [Export]
    public bool LoopPreview { get; set; } = true;

    /// <summary>预览每组行数，用于检查不同容量下的挂点。</summary>
    [Export(PropertyHint.Range, "1,3,1")]
    public int PreviewGroupRows
    {
        get => _previewGroupRows;
        set
        {
            _previewGroupRows = Mathf.Clamp(value, 1, 3);
            if (!VfxPreviewSupport.IsPreviewRun(this)) return;
            _groups = [];
            Replay();
        }
    }

    public override void _Ready()
    {
        // 仅 F6 预览自播；游戏内等待真实布局，避免假布局闪帧。
        if (VfxPreviewSupport.IsPreviewRun(this))
        {
            Replay();
        }
    }

    public override void _Process(double delta)
    {
        if (_groups.Length == 0)
        {
            return;
        }

        _time += (float)delta;
        QueueRedraw();
    }

    /// <summary>接收 NPerformArea 的分组行布局。</summary>
    public void SetGroups(IReadOnlyList<PerformRowGroup> groups)
    {
        _groups = groups.ToArray();
        _previewRows = false;
        QueueRedraw();
    }

    public void PlayShow()
    {
        _hiding = false;
        Visible = true;
        _fadeTween?.Kill();
        _fadeTween = CreateTween();
        _fadeTween.TweenMethod(Callable.From<float>(value =>
        {
            _fade = value;
            QueueRedraw();
        }), 0f, 1f, FadeInDuration);
    }

    public void PlayHide()
    {
        if (_hiding) return;

        _hiding = true;
        _fadeTween?.Kill();
        _fadeTween = CreateTween();
        _fadeTween.TweenMethod(Callable.From<float>(value =>
        {
            _fade = 1f - value;
            QueueRedraw();
        }), 0f, 1f, FadeOutDuration);
        _fadeTween.TweenCallback(Callable.From(() =>
        {
            if (VfxPreviewSupport.IsPreviewRun(this) || LoopPreview && VfxPreviewSupport.IsPreview(this))
            {
                Visible = false;
                _hiding = false;
                return;
            }

            this.QueueFreeSafely();
        }));
    }

    public void Replay()
    {
        if (_groups.Length == 0)
        {
            _groups = BuildPreviewGroups(_previewGroupRows);
            _previewRows = true;
        }

        _fade = 0f;
        _hiding = false;
        Visible = true;
        VfxPreviewSupport.CenterForPreview(this);
        PlayShow();
    }

    public override void _Draw()
    {
        if (_groups.Length == 0)
        {
            return;
        }

        var breath = 0.5f + 0.5f * Mathf.Sin(_time * Mathf.Tau / BreathPeriod);

        // 预览行与游戏内节点层级保持一致。
        if (_previewRows)
        {
            DrawPreviewRowPlaceholders();
        }

        DrawColumn(mirrored: false, shimmerPhase: 0f, breath);
        DrawColumn(mirrored: true, shimmerPhase: 0.5f, breath);
    }

    /// <summary>绘制单列覆膜、提线与吊线。</summary>
    private void DrawColumn(bool mirrored, float shimmerPhase, float breath)
    {
        var hasRows = false;
        var top = float.MaxValue;
        var bottom = float.MinValue;
        var outer = mirrored ? float.MinValue : float.MaxValue;

        foreach (var group in _groups)
        {
            if (group.Mirrored != mirrored) continue;
            foreach (var row in group.Rows)
            {
                var rect = row.Rect;
                if (!hasRows)
                {
                    top = rect.Position.Y;
                    bottom = rect.End.Y;
                    outer = OuterEdgeX(rect, mirrored);
                    hasRows = true;
                    continue;
                }

                top = Mathf.Min(top, rect.Position.Y);
                bottom = Mathf.Max(bottom, rect.End.Y);
                // 行宽不一，提线取同列最外缘。
                outer = mirrored
                    ? Mathf.Max(outer, OuterEdgeX(rect, mirrored))
                    : Mathf.Min(outer, OuterEdgeX(rect, mirrored));
            }
        }

        if (!hasRows) return;

        var wireX = mirrored ? outer + WireOffset : outer - WireOffset;
        var alpha = Mathf.Lerp(0.88f, 1f, breath);

        foreach (var group in _groups)
        {
            if (group.Mirrored != mirrored) continue;
            foreach (var row in group.Rows)
            {
                DrawRowFilm(row, mirrored, alpha);
            }
        }

        DrawWire(wireX, top, bottom, alpha, shimmerPhase);

        foreach (var group in _groups)
        {
            if (group.Mirrored != mirrored) continue;
            foreach (var row in group.Rows)
            {
                DrawRowTie(row, mirrored, wireX, alpha);
            }
        }
    }

    private static float OuterEdgeX(Rect2 row, bool mirrored)
    {
        return mirrored ? row.End.X : row.Position.X;
    }

    /// <summary>绘制斜切覆膜与顶边冷光。</summary>
    private void DrawRowFilm(PerformRow row, bool mirrored, float breathScale)
    {
        PerformRowShape.Fill(this, row.Rect, mirrored, row.Tilt,
            new Color(FilmColor, FilmAlpha * _fade * breathScale), _quadBuffer);

        PerformRowShape.GetTopEdge(row.Rect, mirrored, row.Tilt, out var from, out var to);
        DrawLineGlow(from, to, TieColor, 1f, RimAlpha * _fade * breathScale);
    }

    /// <summary>绘制渐隐提线与下滑高光。</summary>
    private void DrawWire(float x, float top, float bottom, float breath, float shimmerPhase)
    {
        var alpha = WireAlpha * _fade * breath;
        if (alpha <= 0.001f) return;

        var barY = top - WireFadeAbove;

        const int fadeSteps = 6;
        for (var i = 0; i < fadeSteps; i++)
        {
            var t0 = i / (float)fadeSteps;
            var t1 = (i + 1) / (float)fadeSteps;
            DrawLineGlow(
                new Vector2(x, Mathf.Lerp(barY, top, t0)),
                new Vector2(x, Mathf.Lerp(barY, top, t1)),
                WireColor, WireWidth, alpha * Mathf.Lerp(0.03f, 0.9f, t1 * t1), cord: true);
        }

        DrawLineGlow(new Vector2(x, top), new Vector2(x, bottom), WireColor, WireWidth, alpha, cord: true);

        const int tailSteps = 2;
        for (var i = 0; i < tailSteps; i++)
        {
            var t0 = i / (float)tailSteps;
            var t1 = (i + 1) / (float)tailSteps;
            DrawLineGlow(
                new Vector2(x, Mathf.Lerp(bottom, bottom + WireFadeBelow, t0)),
                new Vector2(x, Mathf.Lerp(bottom, bottom + WireFadeBelow, t1)),
                WireColor, WireWidth, alpha * Mathf.Lerp(0.55f, 0.05f, t1), cord: true);
        }

        var progress = Mathf.PosMod(_time / ShimmerPeriod + shimmerPhase, 1f);
        var shimmerAlpha = alpha * 0.30f * Mathf.Sin(progress * Mathf.Pi);
        if (shimmerAlpha <= 0.001f) return;

        var mid = Mathf.Lerp(barY, bottom + WireFadeBelow, progress);
        var half = ShimmerLength * 0.5f;
        DrawLineGlow(new Vector2(x, mid - half), new Vector2(x, mid + half),
            TieColor, WireWidth * 1.8f, shimmerAlpha);
    }

    /// <summary>绘制斜向吊线与环结，钩点跟随斜切顶边。</summary>
    private void DrawRowTie(PerformRow row, bool mirrored, float wireX, float breath)
    {
        var rect = row.Rect;
        var shift = PerformRowShape.TopShift(rect);
        var top = rect.Position.Y;
        var hook = new Vector2(
            mirrored ? rect.End.X - shift - TieInset : rect.Position.X + shift + TieInset, top + 1f);

        var angle = PerformRowShape.VisualTilt(row.Tilt, mirrored);
        if (Mathf.Abs(angle) > 0.0001f)
        {
            var pivot = PerformRowShape.HangPivot(rect, mirrored);
            hook = pivot + (hook - pivot).Rotated(angle);
        }

        var alpha = TieAlpha * _fade * breath;
        DrawLineGlow(new Vector2(wireX, hook.Y - TieDrop), hook, TieColor, TieWidth, alpha, cord: true);
        DrawKnot(hook.X, hook.Y, KnotAlpha * _fade * breath);
    }

    /// <summary>静态发光参数，避免逐帧分配。</summary>
    private static readonly (float Factor, float Scale)[] GlowPasses =
    [
        (4.0f, 0.10f), (2.2f, 0.18f), (1.0f, 1.0f)
    ];

    private readonly Vector2[] _segmentBuffer = new Vector2[2];
    private readonly Vector2[] _quadBuffer = new Vector2[4];
    private readonly Vector2[] _outlineBuffer = new Vector2[5];

    /// <summary>绘制发光线；cord 表示需要暗色底边。</summary>
    private void DrawLineGlow(Vector2 from, Vector2 to, Color color, float width, float alpha, bool cord = false)
    {
        if (alpha <= 0.001f) return;
        _segmentBuffer[0] = from;
        _segmentBuffer[1] = to;
        if (cord)
        {
            DrawPolyline(_segmentBuffer, new Color(CordShadowColor, alpha * 0.55f), width * 2.4f, true);
        }

        DrawLineGlowPasses(_segmentBuffer, color, width, alpha);
    }

    private void DrawLineGlowPasses(Vector2[] points, Color color, float width, float alpha)
    {
        foreach (var (factor, scale) in GlowPasses)
        {
            DrawPolyline(points, new Color(color, Mathf.Min(1f, alpha * scale)), width * factor, true);
        }
    }

    /// <summary>绘制环结，避免实心菱形被读成箭头。</summary>
    private void DrawKnot(float x, float y, float alpha)
    {
        if (alpha <= 0.001f) return;

        var center = new Vector2(x, y);
        var color = new Color(KnotColor, Mathf.Min(1f, alpha * 1.15f));
        DrawArc(center, KnotSize, 0f, Mathf.Tau, 12, color, 1.3f, true);
        DrawCircle(center, KnotSize * 0.34f, new Color(KnotColor, alpha * 0.9f));
    }

    /// <summary>按 NPerformArea 几何生成预览行；布局变化时需同步。</summary>
    private static PerformRowGroup[] BuildPreviewGroups(int rowCount)
    {
        const float itemWidth = 112f;   // NPerformItem.MinimumItemWidth 160 × ItemScale 0.7
        const float itemHeight = 35f;   // NPerformItem.ItemHeight 50 × ItemScale 0.7
        const float rowStep = 49f;      // NPerformArea.ItemSpacing 70 × ItemScale 0.7
        const float columnGap = 10f;    // NPerformArea.SlotHitboxGap
        const float hitboxInset = 32f;  // GetItemPosition 里命中框顶/底的内收量（槽位原点在行高一半处）
        const float halfHitWidth = 160f;
        const float halfHitHeight = 150f;

        var groups = new List<PerformRowGroup>();
        var left = -halfHitWidth - columnGap - itemWidth;
        var right = halfHitWidth + columnGap;
        var topY = -halfHitHeight + hitboxInset - itemHeight / 2f;
        var bottomY = halfHitHeight - hitboxInset - itemHeight / 2f;

        foreach (var (chord, baseSlot, x, startY, step, mirrored) in
                 new (PerformChord, int, float, float, float, bool)[]
                 {
                     (PerformChord.C, 1, left, topY, rowStep, false),
                     (PerformChord.D, 4, left, bottomY, -rowStep, false),
                     (PerformChord.F, 7, right, topY, rowStep, true),
                     (PerformChord.G, 10, right, bottomY, -rowStep, true)
                 })
        {
            var rows = new PerformRow[rowCount];
            for (var i = 0; i < rowCount; i++)
            {
                // 预览与游戏内共用 PerformHangStyle。
                var slotIndex = baseSlot + i;
                var offset = PerformHangStyle.Offset(slotIndex, mirrored);
                rows[i] = new PerformRow(
                    new Rect2(x + offset.X, startY + step * i + offset.Y, itemWidth, itemHeight),
                    PerformHangStyle.TiltRadians(slotIndex));
            }

            groups.Add(new PerformRowGroup(chord, rows, mirrored));
        }

        return groups.ToArray();
    }

    /// <summary>绘制预览用斜切行。</summary>
    private void DrawPreviewRowPlaceholders()
    {
        foreach (var group in _groups)
        {
            var color = PerformChordColors.ColorOf(group.Group);
            foreach (var row in group.Rows)
            {
                PerformRowShape.Fill(this, row.Rect, group.Mirrored, row.Tilt,
                    new Color(color, 0.18f * _fade), _quadBuffer);
                PerformRowShape.Outline(this, row.Rect, group.Mirrored, row.Tilt,
                    new Color(color, 0.75f * _fade), 1.5f, _outlineBuffer);
            }
        }
    }
}
