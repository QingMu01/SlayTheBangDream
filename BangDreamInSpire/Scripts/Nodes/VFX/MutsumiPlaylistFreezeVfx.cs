using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Mechanics.Perform;
using BangDreamInSpire.Scripts.Utils.Infos;
using BangDreamInSpire.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace BangDreamInSpire.Scripts.Nodes.VFX;

/// <summary>
/// 歌单冻结 · 提线悬停（方案 B，2026-11-14 批准）。
/// <para>
/// 墨缇丝形态下歌单被封：四个和弦分组各被一根主提线吊住，组横杆下再垂下三根吊线，
/// 各自钩在该组三条播放列表行的**内端**——「这些行现在挂在别人的线上」。行本身不倾斜
/// （行由 <c>NPerformArea</c> 的布局缓动与镜像管理，接管旋转会与缓动冲突），靠线的下垂感与吊点高光表达被悬挂。
/// </para>
/// <para>
/// 坐标：全部使用 <c>ItemContainer</c> 局部坐标（与 <c>NPerformItem.Position</c> 同一空间），
/// 由 <c>NPerformArea</c> 在布局变化后通过 <see cref="SetGroups" /> 重新投喂，因此槽位增减、演奏区重建后自动贴合。
/// </para>
/// <para>
/// 设计契约见 <c>.scratch/vfx/plans/mutsumi-freeze-playlist.md</c>；层级：提线 z 序低于分组激活角标。
/// </para>
/// </summary>
[Tool]
public partial class MutsumiPlaylistFreezeVfx : Node2D
{
    /// <summary>挂在 <c>ItemContainer</c> 下的固定节点名。</summary>
    public const string NodeName = "PlaylistFreezeVfx";

    /// <summary>一个分组的行布局：分组和弦、三条行的矩形（容器空间）、是否右侧镜像。</summary>
    public readonly record struct RowGroup(PerformChord Group, Rect2[] Rows, bool Mirrored);

    private const float FadeInDuration = 0.25f;
    private const float FadeOutDuration = 0.30f;
    private const float ThreadAlpha = 0.55f;
    private const float CrossbarAlpha = 0.70f;
    private const float HangerAlpha = 0.40f;
    private const float KnotAlpha = 0.45f;
    private const float WashAlpha = 0.10f;
    private const float ThreadWidth = 1.8f;
    private const float HangerWidth = 1.2f;
    private const float CrossbarWidth = 2.6f;
    private const float CrossbarLength = 34f;
    private const float KnotSize = 6f;
    private const float SwayAmplitude = 1.5f;
    private const float BreathPeriod = 1.8f;
    /// <summary>
    /// 主线上端距组包围盒顶边的距离。计划里写的是 60px，但 60px 的短桩读不出「从上方落下来」；
    /// 按批准的预演图（线自画面上方垂下）加长到 200px，顶端做 alpha 渐隐。见 tuning-log。
    /// </summary>
    private const float ThreadTopOffset = 200f;

    /// <summary>各分组主提线在行宽上的锚点比例（同列两组必须错开，否则两条线会完全重合）。</summary>
    private static readonly Dictionary<PerformChord, float> AnchorRatios = new()
    {
        [PerformChord.C] = 0.18f,
        [PerformChord.D] = 0.78f,
        [PerformChord.F] = 0.22f,
        [PerformChord.G] = 0.82f
    };

    private static readonly Color ThreadColor = new("#ded8f6");
    private static readonly Color CrossbarColor = new("#f2eeff");
    private static readonly Color KnotColor = new("#efeaff");
    private static readonly Color WashColor = new("#b9a6ff");

    private RowGroup[] _groups = [];
    private float _fade;
    private float _time;
    private Tween? _fadeTween;
    private Texture2D? _washTexture;
    private bool _previewRows;
    private bool _hiding;

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

    /// <summary>预览态下循环播放（游戏内为持续态，由召唤方控制显隐）。</summary>
    [Export]
    public bool LoopPreview { get; set; } = true;

    public override void _Ready()
    {
        _washTexture = BangDreamPreloadManager.GetTexture2D(
            "res://BangDreamInSpire/images/vfx/flying_music_note/glow_soft.png");

        // 本特效由 NPerformArea 外部驱动：只有「F6 单独运行场景」的预览才自播，
        // 游戏内必须等调用方投喂真实行坐标（否则会先用假布局闪一帧）。
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

    /// <summary>投喂分组行布局（容器局部坐标）。由 <c>NPerformArea</c> 在布局变化后调用。</summary>
    public void SetGroups(IReadOnlyList<RowGroup> groups)
    {
        _groups = groups.ToArray();
        _previewRows = false;
        QueueRedraw();
    }

    /// <summary>冻结开始：0.25s 淡入（组间错开由绘制阶段的 alpha 进度表达）。</summary>
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

    /// <summary>解冻：0.30s 淡出后自毁（或预览态隐藏）。</summary>
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

    /// <summary>预览用：F6 单独运行场景时用假行布局自播。</summary>
    public void Replay()
    {
        if (_groups.Length == 0)
        {
            _groups = BuildPreviewGroups();
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

        var sway = Mathf.Sin(_time * Mathf.Tau / BreathPeriod) * SwayAmplitude;
        var breath = 1f + 0.08f * (0.5f + 0.5f * Mathf.Sin(_time * Mathf.Tau / 2.4f));

        DrawWash(breath);

        foreach (var group in _groups)
        {
            if (group.Rows.Length == 0) continue;

            var ratio = AnchorRatios.TryGetValue(group.Group, out var value) ? value : 0.5f;
            var first = group.Rows[0];
            var top = float.MaxValue;
            var bottom = float.MinValue;
            foreach (var row in group.Rows)
            {
                top = Mathf.Min(top, row.Position.Y);
                bottom = Mathf.Max(bottom, row.End.Y);
            }

            var anchorX = first.Position.X + first.Size.X * ratio;
            var crossbarY = top - 16f;
            var threadTopY = top - ThreadTopOffset;

            // 主线（略带摆动，暗示被提着）；分成三段、越靠上越淡，读作从画面上方落下来
            var alpha = ThreadAlpha * _fade * breath;
            var segments = 3;
            for (var seg = 0; seg < segments; seg++)
            {
                var t0 = seg / (float)segments;
                var t1 = (seg + 1) / (float)segments;
                var p0 = new Vector2(anchorX + sway * t0 * 0.4f, Mathf.Lerp(threadTopY, crossbarY, t0));
                var p1 = new Vector2(anchorX + sway * t1, Mathf.Lerp(threadTopY, crossbarY, t1));
                DrawLineGlow([p0, p1], ThreadColor, ThreadWidth, alpha * Mathf.Lerp(0.15f, 1f, t1));
            }

            // 横杆
            DrawLineGlow(
                [new Vector2(anchorX + sway - CrossbarLength / 2f, crossbarY),
                 new Vector2(anchorX + sway + CrossbarLength / 2f, crossbarY)],
                CrossbarColor, CrossbarWidth, CrossbarAlpha * _fade * breath);

            // 吊线 + 吊点：落在每条行的内端（靠近命中框那一侧，镜像列取右侧）
            foreach (var row in group.Rows)
            {
                var hookX = group.Mirrored ? row.End.X - 12f : row.Position.X + 12f;
                var hookY = row.Position.Y - 4f;
                DrawLineGlow(
                    [new Vector2(anchorX + sway, crossbarY), new Vector2(anchorX + sway, hookY),
                     new Vector2(hookX, hookY)],
                    ThreadColor, HangerWidth, HangerAlpha * _fade * breath);
                DrawKnot(hookX, hookY, KnotAlpha * _fade);
            }

            // 组位置的低亮度底光，避免「行自己消失了」的错觉
            var center = new Vector2(first.Position.X + first.Size.X / 2f, (top + bottom) / 2f);
            DrawCircle(center, 34f, new Color(ThreadColor, 0.05f * _fade));
        }

        if (_previewRows)
        {
            DrawPreviewRowPlaceholders();
        }
    }

    private void DrawWash(float breath)
    {
        if (_washTexture == null || _groups.Length == 0)
        {
            return;
        }

        var bounds = GetBounds();
        if (bounds.Size.X <= 0f || bounds.Size.Y <= 0f)
        {
            return;
        }

        var rect = bounds.Grow(40f);
        var color = new Color(WashColor, WashAlpha * _fade * breath);
        DrawTextureRect(_washTexture, rect, false, color);
    }

    private Rect2 GetBounds()
    {
        var bounds = new Rect2();
        var first = true;
        foreach (var group in _groups)
        {
            foreach (var row in group.Rows)
            {
                if (first)
                {
                    bounds = row;
                    first = false;
                }
                else
                {
                    bounds = bounds.Merge(row);
                }
            }
        }

        return bounds;
    }

    private void DrawLineGlow(Vector2[] points, Color color, float width, float alpha)
    {
        if (alpha <= 0.001f) return;

        // 外发光：同一条折线描三遍、由宽到窄、由低到高 alpha
        foreach (var (factor, scale) in new[] { (4.0f, 0.10f), (2.2f, 0.18f), (1.0f, 1.0f) })
        {
            DrawPolyline(points, new Color(color, Mathf.Min(1f, alpha * scale)), width * factor, true);
        }
    }

    private void DrawKnot(float x, float y, float alpha)
    {
        if (alpha <= 0.001f) return;

        var points = new[]
        {
            new Vector2(x, y - KnotSize), new Vector2(x + KnotSize, y),
            new Vector2(x, y + KnotSize), new Vector2(x - KnotSize, y)
        };
        DrawColoredPolygon(points, new Color(KnotColor, alpha));
        DrawPolyline([points[0], points[1], points[2], points[3], points[0]],
            new Color(KnotColor, Mathf.Min(1f, alpha * 1.2f)), 1.2f, true);
    }

    /// <summary>预览态用假行：按 NPerformArea 的真实几何（112×35、行距 49、两列、右侧镜像）造 12 行。</summary>
    private static RowGroup[] BuildPreviewGroups()
    {
        const float itemWidth = 112f;
        const float itemHeight = 35f;
        const float rowStep = 49f;
        const float columnGap = 10f;
        const float halfHitWidth = 160f;
        const float halfHitHeight = 150f;

        var groups = new List<RowGroup>();
        var left = -halfHitWidth - columnGap - itemWidth;
        var right = halfHitWidth + columnGap;
        var topY = -halfHitHeight + 32f;
        var bottomY = halfHitHeight - 32f - itemHeight;

        foreach (var (chord, x, startY, step, mirrored) in new (PerformChord, float, float, float, bool)[]
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

            groups.Add(new RowGroup(chord, rows, mirrored));
        }

        return groups.ToArray();
    }

    /// <summary>预览态下把行本身也画出来（游戏内行由 NPerformArea 显示，不在这里画）。</summary>
    private void DrawPreviewRowPlaceholders()
    {
        foreach (var group in _groups)
        {
            var color = PerformChordColors.ColorOf(group.Group);
            foreach (var row in group.Rows)
            {
                DrawRect(row, new Color(color, 0.18f * _fade), true);
                DrawRect(row, new Color(color, 0.75f * _fade), false, 1.5f);
            }
        }
    }
}
