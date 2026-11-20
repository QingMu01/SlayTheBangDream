using BangDreamInSpire.Scripts.Enums;
using BangDreamInSpire.Scripts.Interfaces.Mechanics;
using BangDreamInSpire.Scripts.Utils.Infos;
using BangDreamInSpire.Scripts.Utils;
using Godot;
using ItsCrychic.Scripts.Utils;
using MegaCrit.Sts2.Core.Helpers;

namespace ItsCrychic.Scripts.Nodes.VFX;

/// <summary>
/// 歌单冻结 · 提线悬停。
/// <para>
/// 墨缇丝形态下歌单被封：左右两列各被一根提线吊住（线走列的外侧留白），同列上下两组共用这根线，
/// 每个分组的播放列表行再用一小段斜向吊线拴到线上、拴点打一个环结——
/// 「这些行现在挂在别人的线上」。行本身不倾斜（行由 <c>NPerformArea</c> 的布局缓动与镜像管理，
/// 接管旋转会与缓动冲突），靠提线/吊线的牵引、以及行顶边走的一道冷光表达被悬挂。
/// </para>
/// <para>
/// 四角布局的同列上下两组在满容量时只隔约 5px；每组一根长线会横穿上组的行，
/// 或从角色身侧跨过。每列共用一根外侧提线，可在任何容量（1–3 行）下避开行与角色。
/// </para>
/// <para>
/// 坐标：全部使用 <c>ItemContainer</c> 局部坐标（与 <c>NPerformItem.Position</c> 同一空间），
/// 由 <c>NPerformArea</c> 在布局变化后通过 <see cref="SetGroups" /> 重新投喂，因此槽位增减、演奏区重建后自动贴合。
/// 行是斜切平行四边形（<c>parallelogram_clip.gdshader</c>，<c>slant_slope = 0.3</c>），
/// 覆膜、钩点与顶边光都按斜边计算，不能按矩形画。
/// </para>
/// <para>
/// 层级：提线 z 序低于分组激活角标。
/// </para>
/// </summary>
[Tool]
public partial class MutsumiPlaylistFreezeVfx : Node2D, IPerformAreaFreezeVisual
{
    /// <summary>挂在 <c>ItemContainer</c> 下的固定节点名。</summary>
    public const string NodeName = "PlaylistFreezeVfx";

    public Node2D View => this;

    private const float FadeInDuration = 0.25f;
    private const float FadeOutDuration = 0.30f;

    /// <summary>列外侧那根提线的不透明度。</summary>
    private const float WireAlpha = 0.55f;
    private const float TieAlpha = 0.45f;
    private const float KnotAlpha = 0.60f;
    private const float RimAlpha = 0.12f;
    private const float FilmAlpha = 0.06f;
    private const float WireWidth = 1.6f;
    private const float TieWidth = 1.2f;
    private const float KnotSize = 3.6f;
    private const float BreathPeriod = 1.8f;

    /// <summary>提线上端续出的渐隐段：读作「从上方落下来」，又不跨过行与角色。</summary>
    private const float WireFadeAbove = 160f;
    private const float WireFadeBelow = 14f;

    /// <summary>
    /// 提线与列外缘的水平间距。行在卡面相片（160 宽）之外还探出一条 165×25 的标题底色带
    /// （约 14px），提线要落在带子外面才不会被它压住。
    /// </summary>
    private const float WireOffset = 18f;

    /// <summary>吊线钩点从行外缘内缩的深度。</summary>
    private const float TieInset = 3.5f;

    /// <summary>吊线在行顶边上方起钩的高度。</summary>
    private const float TieDrop = 11f;

    /// <summary>沿提线缓慢下滑的高光：半段长度与周期。</summary>
    private const float ShimmerLength = 26f;
    private const float ShimmerPeriod = 2.6f;

    private static readonly Color WireColor = new("#ded8f6");
    private static readonly Color TieColor = new("#f2eeff");
    private static readonly Color KnotColor = new("#efeaff");
    private static readonly Color FilmColor = new("#b9a6ff");

    /// <summary>提线/吊线底下的暗色描边，保证浅色战斗背景上也有清晰轮廓。</summary>
    private static readonly Color CordShadowColor = new("#0a0812");

    private PerformRowGroup[] _groups = [];
    private float _fade;
    private float _time;
    private Tween? _fadeTween;
    private bool _previewRows;
    private bool _hiding;
    private int _previewGroupRows = 3;

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

    /// <summary>
    /// 预览态下每组摆几行：游戏内初始每组 1 行、上限 3 行，提线要在这三种容量下都贴得住。
    /// 在 Inspector 里改会立即重播，用来看各容量下的挂点。
    /// </summary>
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
    public void SetGroups(IReadOnlyList<PerformRowGroup> groups)
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

        // 预览态先把占位行画出来，让提线/吊线叠在行上，与游戏内的节点层级一致。
        if (_previewRows)
        {
            DrawPreviewRowPlaceholders();
        }

        // 两列各挂一根提线：同列上下两组共用一根线，线走列的外侧留白，
        // 不跨过任何一行、也不压到角色身上（四角布局里中间只有角色）。
        DrawColumn(mirrored: false, shimmerPhase: 0f, breath);
        DrawColumn(mirrored: true, shimmerPhase: 0.5f, breath);
    }

    /// <summary>画一列：行的覆膜与冷边、外侧提线、每行的吊线与环结。</summary>
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
                // 行宽按卡名自适应，同列各行外缘不一定齐平：提线取最外侧，才不会压在行的斜边上。
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

        // 吊线画在提线之后：接触点压在线上，读作「线从提线里分出来」。
        foreach (var group in _groups)
        {
            if (group.Mirrored != mirrored) continue;
            foreach (var row in group.Rows)
            {
                DrawRowTie(row, mirrored, wireX, alpha);
            }
        }
    }

    /// <summary>行在视觉上最靠外（远离角色）的那条边；行宽随卡名自适应，所以逐行取。</summary>
    private static float OuterEdgeX(Rect2 row, bool mirrored)
    {
        return mirrored ? row.End.X : row.Position.X;
    }

    /// <summary>行覆膜：按斜切四边形填一层冷色，再沿顶边描一道冷光，读作「被提起来的那条边」。</summary>
    private void DrawRowFilm(PerformRow row, bool mirrored, float breathScale)
    {
        PerformRowShape.Fill(this, row.Rect, mirrored, row.Tilt,
            new Color(FilmColor, FilmAlpha * _fade * breathScale), _quadBuffer);

        PerformRowShape.GetTopEdge(row.Rect, mirrored, row.Tilt, out var from, out var to);
        DrawLineGlow(from, to, TieColor, 1f, RimAlpha * _fade * breathScale);
    }

    /// <summary>提线本体：上端长渐隐、中间实线、下端短渐隐，外加一段缓慢下滑的高光。</summary>
    private void DrawWire(float x, float top, float bottom, float breath, float shimmerPhase)
    {
        var alpha = WireAlpha * _fade * breath;
        if (alpha <= 0.001f) return;

        var barY = top - WireFadeAbove;

        // 上端：长渐隐，读作「线从画面上方垂下来」，但不跨过行、也不压角色。
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

        // 缓慢下滑的高光：不改变几何，只让线「活着」，避免冻结时整块画面像截图。
        var progress = Mathf.PosMod(_time / ShimmerPeriod + shimmerPhase, 1f);
        var shimmerAlpha = alpha * 0.30f * Mathf.Sin(progress * Mathf.Pi);
        if (shimmerAlpha <= 0.001f) return;

        var mid = Mathf.Lerp(barY, bottom + WireFadeBelow, progress);
        var half = ShimmerLength * 0.5f;
        DrawLineGlow(new Vector2(x, mid - half), new Vector2(x, mid + half),
            TieColor, WireWidth * 1.8f, shimmerAlpha);
    }

    /// <summary>
    /// 单行的吊线：从上方的提线斜拉到行**顶边外端**（斜切让顶边内缩，钩点要跟着内缩），
    /// 钩点打一个环结。斜拉的短吊线比水平卡扣更像「被线吊着」。
    /// </summary>
    private void DrawRowTie(PerformRow row, bool mirrored, float wireX, float breath)
    {
        var rect = row.Rect;
        var shift = PerformRowShape.TopShift(rect);
        var top = rect.Position.Y;
        var hook = new Vector2(
            mirrored ? rect.End.X - shift - TieInset : rect.Position.X + shift + TieInset, top + 1f);

        // 钩点跟着行一起绕外上角转：行的倾斜不改变「线系在哪里」，只是那一点跟着动。
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

    /// <summary>外发光的固定三遍参数；提成静态表，避免每帧每段线都分配。</summary>
    private static readonly (float Factor, float Scale)[] GlowPasses =
    [
        (4.0f, 0.10f), (2.2f, 0.18f), (1.0f, 1.0f)
    ];

    private readonly Vector2[] _segmentBuffer = new Vector2[2];
    private readonly Vector2[] _quadBuffer = new Vector2[4];
    private readonly Vector2[] _outlineBuffer = new Vector2[5];

    /// <summary>描一条带外发光的线；<paramref name="cord" /> 为真时先垫一道暗色，浅色背景上也读得出轮廓。</summary>
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
        // 同一条折线描三遍、由宽到窄、由低到高 alpha。
        foreach (var (factor, scale) in GlowPasses)
        {
            DrawPolyline(points, new Color(color, Mathf.Min(1f, alpha * scale)), width * factor, true);
        }
    }

    /// <summary>钩点的绳结：一个小环 + 环心亮点，读作「线从环里穿过去打结」，不要画成实心菱形（像箭头）。</summary>
    private void DrawKnot(float x, float y, float alpha)
    {
        if (alpha <= 0.001f) return;

        var center = new Vector2(x, y);
        var color = new Color(KnotColor, Mathf.Min(1f, alpha * 1.15f));
        DrawArc(center, KnotSize, 0f, Mathf.Tau, 12, color, 1.3f, true);
        DrawCircle(center, KnotSize * 0.34f, new Color(KnotColor, alpha * 0.9f));
    }

    /// <summary>
    /// 预览态用假行：照当前四角布局复刻 <c>NPerformArea.GetItemPosition</c> 的几何
    /// （160×50 局部 → 0.7 缩放后 112×35、行距 49、四组贴命中框四角、右侧列镜像、组间留 5px 缝）。
    /// 改动演奏区布局时同步这里，否则 F6 预览与游戏内不是同一个形态。
    /// </summary>
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
                // 冻结落位与倾角直接查核心那张表（PerformHangStyle），预览和游戏内才不会是两套数。
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

    /// <summary>预览态下把行本身也画出来（游戏内行由 NPerformArea 显示，不在这里画），按斜切平行四边形画。</summary>
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
