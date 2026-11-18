using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Mechanics.Perform;
using BangDreamInSpire.Scripts.Utils.Infos;
using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamInSpire.Scripts.Nodes.VFX;

/// <summary>
/// 分组激活 · 提线收紧（方案 B，2026-11-14 批准）。
/// <para>
/// 墨缇丝「演奏」不发动效果，而是把一个和弦分组点亮：该组三条播放列表行同时亮起描边，
/// 并从这个分组角标拉出一根绷紧的提线指向角色本人，激活瞬间有一个光点沿线跑回去——
/// 四组齐备时四根线同闪一次，为「小睦被拉回场上」做前置铺垫。
/// </para>
/// <para>
/// 一次性部分 LIFE = 0.40s；常驻部分（行描边 + 提线）持续到解冻或战斗结束。
/// 设计契约见 <c>.scratch/vfx/plans/mutsumi-group-activation.md</c>。
/// </para>
/// </summary>
[Tool]
public partial class MutsumiGroupActivationVfx : Node2D
{
    /// <summary>挂在 <c>ItemContainer</c> 下的固定节点名前缀（实际节点为 <c>GroupActivationVfx</c>）。</summary>
    public const string NodeName = "GroupActivationVfx";

    /// <summary>一个已激活分组的布局与端点（容器局部坐标）。</summary>
    public readonly record struct GroupState(
        PerformChord Group,
        Rect2[] Rows,
        Vector2 BadgeCenter,
        Vector2 TargetCenter,
        bool Mirrored);

    /// <summary>一次性激活表现的时长；<c>NPerformArea</c> 用它把多组点亮串行排队。</summary>
    public const float OneShotLifeTime = 0.40f;

    private const float LifeTime = OneShotLifeTime;
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

    private readonly Dictionary<PerformChord, GroupState> _active = [];
    private GroupState? _shot;
    private float _shotElapsed;
    /// <summary>吊线外发光的固定三遍参数；提成静态表，避免每帧每条线都分配。</summary>
    private static readonly (float Width, float Scale)[] GlowPasses =
    [
        (8f, 0.10f), (4f, 0.20f), (2f, 1f)
    ];

    private readonly Vector2[] _threadBuffer = new Vector2[3];

    private float _time;
    private Texture2D? _ringTexture;
    private Texture2D? _starTexture;
    private Texture2D? _glintTexture;
    private bool _previewRows;

    /// <summary>勾选即重播一次（自动复位，便于反复触发）。</summary>
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

    /// <summary>预览态下循环播放。</summary>
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

    /// <summary>投喂全部已激活分组（幂等：每次变化整体重建常驻层）。</summary>
    public void SetActive(IReadOnlyList<GroupState> states)
    {
        _active.Clear();
        foreach (var state in states)
        {
            _active[state.Group] = state;
        }

        QueueRedraw();
    }

    /// <summary>播放某个分组的一次性激活表现（0.40s）。</summary>
    public void PlayOneShot(GroupState state)
    {
        _shot = state;
        _shotElapsed = 0f;
        QueueRedraw();
    }

    /// <summary>清空常驻层（解冻 / 复活）。</summary>
    public void Clear()
    {
        _active.Clear();
        _shot = null;
        _shotElapsed = 0f;
        QueueRedraw();
    }

    /// <summary>预览用：F6 单独运行场景时造四组假行，C/G 常驻点亮，一次性在两者间轮播。</summary>
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

        // 常驻：行描边 + 提线
        foreach (var state in _active.Values)
        {
            var color = PerformChordColors.ColorOf(state.Group);
            DrawRowsLit(state.Rows, Mathf.Lerp(RowsHoldAlpha * 0.85f, RowsHoldAlpha, breath), 1f, color);
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

        // 一次性：行点亮 → 扫光 → 提线长出 → 环 + 星 → 光点沿线跑回
        var shot = _shot.Value;
        var rowsLit = Mathf.Clamp(_shotElapsed / RowsLitDuration, 0f, 1f);
        var grow = Mathf.Clamp(_shotElapsed / ThreadGrowDuration, 0f, 1f);
        var shotColor = PerformChordColors.ColorOf(shot.Group);
        DrawRowsLit(shot.Rows, 1f, rowsLit, shotColor, highlight: true);
        DrawThread(shot.BadgeCenter, shot.TargetCenter, grow, grow, shotColor);
        DrawSweep(shot.Rows, _shotElapsed);
        DrawRing(shot.BadgeCenter);
        DrawStar(shot.BadgeCenter);
        DrawPulse(shot.BadgeCenter, shot.TargetCenter);
    }

    private void DrawRowsLit(Rect2[] rows, float alpha, float progress, Color color, bool highlight = false)
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
                DrawRect(row, new Color(color, 0.22f * alpha), true);
            }

            DrawRect(row, new Color(color, 0.9f * alpha), false, 2.2f);
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
        // 轻微下垂：中点向重力方向偏移 2px，读作「绷紧但仍有重量」
        mid += new Vector2(0f, 2f);
        _threadBuffer[0] = from;
        _threadBuffer[1] = mid;
        _threadBuffer[2] = end;
        foreach (var (width, scale) in GlowPasses)
        {
            DrawPolyline(_threadBuffer, new Color(color, Mathf.Min(1f, alpha * scale)), width, true);
        }
    }

    private void DrawSweep(Rect2[] rows, float elapsed)
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
            top = Mathf.Min(top, row.Position.Y);
            bottom = Mathf.Max(bottom, row.End.Y);
        }

        var y = Mathf.Lerp(top - 6f, bottom + 6f, t);
        var alpha = Mathf.Sin(t * Mathf.Pi) * 0.9f;
        var color = _shot?.Group is { } group ? PerformChordColors.ColorOf(group) : new Color("#ded8f6");
        var left = float.MaxValue;
        var right = float.MinValue;
        foreach (var row in rows)
        {
            left = Mathf.Min(left, row.Position.X);
            right = Mathf.Max(right, row.End.X);
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

    private List<GroupState> BuildPreviewStates()
    {
        const float itemWidth = 112f;
        const float itemHeight = 35f;
        const float rowStep = 49f;
        const float columnGap = 10f;
        const float halfHitWidth = 160f;
        const float halfHitHeight = 150f;
        var left = -halfHitWidth - columnGap - itemWidth;
        var right = halfHitWidth + columnGap;
        var topY = -halfHitHeight + 32f;
        var bottomY = halfHitHeight - 32f - itemHeight;

        var states = new List<GroupState>();
        foreach (var (group, x, startY, step, mirrored) in new (PerformChord, float, float, float, bool)[]
                 {
                     (PerformChord.C, left, topY, rowStep, false),
                     (PerformChord.D, left, bottomY, -rowStep, false),
                     (PerformChord.F, right, topY, rowStep, true),
                     (PerformChord.G, right, bottomY, -rowStep, true)
                 })
        {
            var rows = new Rect2[3];
            for (var i = 0; i < 3; i++)
            {
                rows[i] = new Rect2(x, startY + step * i, itemWidth, itemHeight);
            }

            var badge = mirrored
                ? new Vector2(x + itemWidth + 14f, rows[0].Position.Y + 8f)
                : new Vector2(x - 14f, rows[0].Position.Y + 8f);
            states.Add(new GroupState(group, rows, badge, Vector2.Zero, mirrored));
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
                DrawRect(row, new Color(color, 0.18f), true);
            }
        }
    }
}
