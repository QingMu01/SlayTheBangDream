using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Interfaces.Mechanics;
using BangDreamInSpire.Scripts.Utils.Infos;
using BangDreamInSpire.Scripts.Utils;
using Godot;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Helpers;

namespace ItsCrychic.Scripts.Nodes.VFX;

/// <summary>分组激活时点亮行并连接角色；一次性动画 0.40s，
/// 常驻层持续到解冻或战斗结束。</summary>
[Tool]
public partial class MutsumiGroupActivationVfx : Node2D, IPerformAreaActivationVisual
{
    public const string NodeName = "GroupActivationVfx";

    public Node2D View => this;

    public float OneShotLifeTime => OneShotLifeTimeSeconds;

    public const float OneShotLifeTimeSeconds = 0.40f;

    private const float LifeTime = OneShotLifeTimeSeconds;
    private const float RowsLitDuration = 0.12f;
    private const float ThreadGrowDuration = 0.18f;
    private const float RingDuration = 0.128f;
    private const float StarDuration = 0.104f;
    private const float PulseStart = 0.10f;
    private const float PulseEnd = 0.36f;
    private const float ThreadAlpha = 0.42f;
    private const float RowsHoldAlpha = 0.62f;
    private const float BreathPeriod = 1.8f;
    private const float RingStartSize = 62f;
    private const float RingEndSize = 104f;
    private const float StarStartSize = 96f;
    private const float StarEndSize = 128f;
    private const float PulseSize = 54f;

    private readonly Dictionary<PerformChord, PerformActivationGroup> _active = [];
    private PerformActivationGroup? _shot;
    private float _shotElapsed;
    /// <summary>静态发光参数，避免逐帧分配。</summary>
    private static readonly (float Width, float Scale)[] GlowPasses =
    [
        (8f, 0.10f), (4f, 0.20f), (2f, 1f)
    ];

    private readonly Vector2[] _threadBuffer = new Vector2[3];
    private readonly Vector2[] _quadBuffer = new Vector2[4];
    private readonly Vector2[] _outlineBuffer = new Vector2[5];

    private float _time;
    private Texture2D? _ringTexture;
    private Texture2D? _starTexture;
    private Texture2D? _glintTexture;
    private bool _previewRows;

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

    public override void _Ready()
    {
        _ringTexture = BangDreamPreloadManager.GetTexture2D(
            "res://BangDreamInSpire/images/vfx/whiteRing.png");
        _starTexture = BangDreamPreloadManager.GetTexture2D(
            "res://BangDreamInSpire/images/vfx/star_322.png");
        _glintTexture = BangDreamPreloadManager.GetTexture2D(
            "res://BangDreamInSpire/images/vfx/star_270_1.png");

        if (VfxPreviewSupport.IsPreviewRun(this))
        {
            Replay();
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;

        if (_shot.HasValue)
        {
            _shotElapsed += (float)delta;
            if (_shotElapsed >= LifeTime)
            {
                _shot = null;
            }
        }

        if (_previewRows && _previewCycleTimer >= 0f)
        {
            _previewCycleTimer += (float)delta;
            if (_previewCycleTimer >= 2.0f)
            {
                _previewCycleTimer = 0f;
                var states = BuildPreviewStates();
                if (states.Count == 4)
                {
                    _previewShowFirst = !_previewShowFirst;
                    PlayOneShot(_previewShowFirst ? states[0] : states[3]);
                }
            }
        }

        if (_active.Count > 0 || _shot.HasValue || _previewRows)
        {
            QueueRedraw();
        }
    }

    /// <summary>整体重建已激活分组的常驻层。</summary>
    public void SetActive(IReadOnlyList<PerformActivationGroup> states)
    {
        _active.Clear();
        foreach (var state in states)
        {
            _active[state.Group] = state;
        }

        QueueRedraw();
    }

    public void PlayOneShot(PerformActivationGroup state)
    {
        _shot = state;
        _shotElapsed = 0f;
        QueueRedraw();
    }

    public void Clear()
    {
        _active.Clear();
        _shot = null;
        _shotElapsed = 0f;
        QueueRedraw();
    }

    public void Replay()
    {
        VfxPreviewSupport.CenterForPreview(this);
        var states = BuildPreviewStates();
        if (states.Count == 0) return;

        _previewRows = _active.Count == 0;
        SetActive([states[0], states[3]]);
        PlayOneShot(states[3]);
        _previewCycleTimer = 0f;
    }

    private float _previewCycleTimer = -1f;
    private bool _previewShowFirst = true;

    public override void _Draw()
    {
        var breath = 0.5f + 0.5f * Mathf.Sin(_time * Mathf.Tau / BreathPeriod);

        foreach (var state in _active.Values)
        {
            var color = PerformChordColors.ColorOf(state.Group);
            DrawRowsLit(state.Rows, state.Mirrored, Mathf.Lerp(RowsHoldAlpha * 0.85f, RowsHoldAlpha, breath),
                1f, color);
            DrawThread(state.BadgeCenter, state.TargetCenter, Mathf.Lerp(ThreadAlpha * 0.9f, ThreadAlpha, breath), 1f, color);
        }

        if (_previewRows)
        {
            DrawPreviewRows();
        }

        if (!_shot.HasValue)
        {
            return;
        }

        var shot = _shot.Value;
        var rowsLit = Mathf.Clamp(_shotElapsed / RowsLitDuration, 0f, 1f);
        var grow = Mathf.Clamp(_shotElapsed / ThreadGrowDuration, 0f, 1f);
        var shotColor = PerformChordColors.ColorOf(shot.Group);
        DrawRowsLit(shot.Rows, shot.Mirrored, 1f, rowsLit, shotColor, highlight: true);
        DrawThread(shot.BadgeCenter, shot.TargetCenter, grow, grow, shotColor);
        DrawSweep(shot.Rows, _shotElapsed);
        DrawRing(shot.BadgeCenter);
        DrawStar(shot.BadgeCenter);
        DrawPulse(shot.BadgeCenter, shot.TargetCenter);
    }

    /// <summary>按斜切四边形绘制行，避免矩形角越界。</summary>
    private void DrawRowsLit(PerformRow[] rows, bool mirrored, float alpha, float progress, Color color,
        bool highlight = false)
    {
        if (rows.Length == 0 || alpha <= 0.001f)
        {
            return;
        }

        var count = Mathf.CeilToInt(progress * rows.Length);
        for (var i = 0; i < rows.Length && i < count; i++)
        {
            var row = rows[i];
            if (highlight)
            {
                PerformRowShape.Fill(this, row.Rect, mirrored, row.Tilt,
                    new Color(color, 0.22f * alpha), _quadBuffer);
            }

            PerformRowShape.Outline(this, row.Rect, mirrored, row.Tilt,
                new Color(color, 0.9f * alpha), 2.2f, _outlineBuffer);
        }
    }

    private void DrawThread(Vector2 from, Vector2 to, float alpha, float progress, Color color)
    {
        if (alpha <= 0.001f || progress <= 0.001f)
        {
            return;
        }

        var end = from.Lerp(to, progress);
        var mid = from.Lerp(to, 0.5f);
        mid += new Vector2(0f, 2f);
        _threadBuffer[0] = from;
        _threadBuffer[1] = mid;
        _threadBuffer[2] = end;
        foreach (var (width, scale) in GlowPasses)
        {
            DrawPolyline(_threadBuffer, new Color(color, Mathf.Min(1f, alpha * scale)), width, true);
        }
    }

    private void DrawSweep(PerformRow[] rows, float elapsed)
    {
        const float start = LifeTime * 0.10f;
        const float end = LifeTime * 0.55f;
        if (elapsed < start || elapsed > end || rows.Length == 0)
        {
            return;
        }

        var t = (elapsed - start) / (end - start);
        var top = float.MaxValue;
        var bottom = float.MinValue;
        foreach (var row in rows)
        {
            top = Mathf.Min(top, row.Rect.Position.Y);
            bottom = Mathf.Max(bottom, row.Rect.End.Y);
        }

        var y = Mathf.Lerp(top - 6f, bottom + 6f, t);
        var alpha = Mathf.Sin(t * Mathf.Pi) * 0.9f;
        var color = _shot?.Group is { } group ? PerformChordColors.ColorOf(group) : new Color("#ded8f6");
        var left = float.MaxValue;
        var right = float.MinValue;
        foreach (var row in rows)
        {
            left = Mathf.Min(left, row.Rect.Position.X);
            right = Mathf.Max(right, row.Rect.End.X);
        }

        for (var i = 0; i < 3; i++)
        {
            var width = 10f - i * 3f;
            DrawLine(new Vector2(left - 6f, y), new Vector2(right + 6f, y),
                new Color(color, alpha * (1f - i * 0.3f)), width);
        }
    }

    private void DrawRing(Vector2 center)
    {
        if (_ringTexture == null) return;

        var t = Mathf.Clamp(_shotElapsed / RingDuration, 0f, 1f);
        if (t >= 1f) return;

        var size = Mathf.Lerp(RingStartSize, RingEndSize, 1f - Mathf.Pow(1f - t, 3f));
        var color = _shot?.Group is { } group ? PerformChordColors.ColorOf(group) : new Color("#ded8f6");
        DrawTextureRect(_ringTexture,
            new Rect2(center - new Vector2(size, size) / 2f, new Vector2(size, size)), false,
            new Color(color, 0.9f * (1f - t)));
    }

    private void DrawStar(Vector2 center)
    {
        if (_starTexture == null) return;

        const float start = LifeTime * 0.02f;
        const float end = LifeTime * 0.26f;
        if (_shotElapsed < start || _shotElapsed > end) return;

        var t = (_shotElapsed - start) / (end - start);
        var size = Mathf.Lerp(StarStartSize, StarEndSize, t);
        var color = _shot?.Group is { } group ? PerformChordColors.ColorOf(group) : new Color("#ded8f6");
        DrawSetTransform(center, Mathf.DegToRad(8f * t), Vector2.One);
        DrawTextureRect(_starTexture, new Rect2(new Vector2(-size, -size) / 2f, new Vector2(size, size)),
            false, new Color(color, 0.85f * (1f - t)));
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private void DrawPulse(Vector2 from, Vector2 to)
    {
        if (_glintTexture == null || !_shot.HasValue) return;

        var start = LifeTime * PulseStart;
        var end = LifeTime * PulseEnd;
        if (_shotElapsed < start) return;

        var t = Mathf.Clamp((_shotElapsed - start) / (end - start), 0f, 1f);
        var pos = from.Lerp(to, t);
        var alpha = (1f - t) * 0.9f;
        DrawTextureRect(_glintTexture,
            new Rect2(pos - new Vector2(PulseSize, PulseSize) / 2f, new Vector2(PulseSize, PulseSize)),
            false, new Color(1f, 1f, 1f, alpha));
    }

    private List<PerformActivationGroup> BuildPreviewStates()
    {
        const float itemWidth = 112f;
        const float itemHeight = 35f;
        const float rowStep = 49f;
        const float columnGap = 10f;
        const float halfHitWidth = 160f;
        const float halfHitHeight = 150f;
        var left = -halfHitWidth - columnGap - itemWidth;
        var right = halfHitWidth + columnGap;
        var topY = -halfHitHeight + 32f - itemHeight / 2f;
        var bottomY = halfHitHeight - 32f - itemHeight / 2f;

        var states = new List<PerformActivationGroup>();
        foreach (var (group, slotBase, x, startY, step, mirrored) in
                 new (PerformChord, int, float, float, float, bool)[]
                 {
                     (PerformChord.C, 1, left, topY, rowStep, false),
                     (PerformChord.D, 4, left, bottomY, -rowStep, false),
                     (PerformChord.F, 7, right, topY, rowStep, true),
                     (PerformChord.G, 10, right, bottomY, -rowStep, true)
                 })
        {
            var rows = new PerformRow[3];
            for (var i = 0; i < 3; i++)
            {
                var slotIndex = slotBase + i;
                var offset = PerformHangStyle.Offset(slotIndex, mirrored);
                rows[i] = new PerformRow(
                    new Rect2(x + offset.X, startY + step * i + offset.Y, itemWidth, itemHeight),
                    PerformHangStyle.TiltRadians(slotIndex));
            }

            var badge = mirrored
                ? new Vector2(x + itemWidth + 14f, rows[0].Rect.Position.Y + 8f)
                : new Vector2(x - 14f, rows[0].Rect.Position.Y + 8f);
            states.Add(new PerformActivationGroup(group, rows, badge, Vector2.Zero, mirrored));
        }

        return states;
    }

    private void DrawPreviewRows()
    {
        foreach (var state in _active.Values)
        {
            var color = PerformChordColors.ColorOf(state.Group);
            foreach (var row in state.Rows)
            {
                PerformRowShape.Fill(this, row.Rect, state.Mirrored, row.Tilt,
                    new Color(color, 0.18f), _quadBuffer);
            }
        }
    }
}
