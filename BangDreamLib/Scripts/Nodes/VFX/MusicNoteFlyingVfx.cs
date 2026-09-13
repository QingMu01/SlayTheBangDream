using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Random;

namespace BangDreamLib.Scripts.Nodes.VFX;

[Tool]
public partial class MusicNoteFlyingVfx : NBangDreamFlyingVfx
{
    private const float BaseFlutterAmplitude = 10f;
    private const float BaseFlutterFrequency = 1.6f;
    private const float MaxCurveDistanceRatio = 0.45f;

    private float _curveDirection = -1f;
    private float _curveScaleMultiplier = 1f;
    private float _curveScale;
    private float _curveStrength = 128f;
    private float _flutterAmplitude;
    private float _flutterFrequency;
    private float _flutterPhase;

    private Sprite2D? _sprites;
    private Tween? _tween;
    private float _trajectoryVariation = 0.7f;

    /// <summary>场景中精灵的原始缩放/着色，重播时恢复到该基准（两个场景基准不同）。</summary>
    private Vector2 _spriteBaseScale = Vector2.One;
    private Color _spriteBaseModulate = Colors.White;

    private Vector2 _startPos;
    private Vector2 _endPos;
    private Vector2 _controlPoint;
    private float _totalDistance;
    private float _travelProgress;
    private bool _hasPath;
    private bool _isMoving;
    private bool _isHit;

    private Vector2 _previewOrigin;

    [Export] public double Speed = 800.0f;

    [Export(PropertyHint.Range, "0,160,1")]
    public float CurveStrength
    {
        get => _curveStrength;
        set
        {
            _curveStrength = value;
            ReplayIfPreviewing();
        }
    }

    [Export(PropertyHint.Range, "0,0.8,0.01")]
    public float TrajectoryVariation
    {
        get => _trajectoryVariation;
        set
        {
            _trajectoryVariation = value;
            ReplayIfPreviewing();
        }
    }

    /// <summary>
    /// 预览是否采用自动计算的演示航程（视口 x 1/4 → 3/4、y 中点），无需手填下面两个偏移。
    /// 关闭后再按 <see cref="PreviewStart" />/<see cref="PreviewEnd" /> 作相对偏移。
    /// </summary>
    [Export] public bool AutoPreviewSpan { get; set; } = true;

    /// <summary>预览飞行起点（相对节点初始位置的偏移）；<see cref="AutoPreviewSpan" /> 开启时忽略。</summary>
    [Export] public Vector2 PreviewStart { get; set; } = new(-500f, 0f);

    /// <summary>预览飞行终点（相对节点初始位置的偏移）；<see cref="AutoPreviewSpan" /> 开启时忽略。</summary>
    [Export] public Vector2 PreviewEnd { get; set; } = new(0f, 0f);

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

    /// <summary>预览态下播放结束后自动重播。</summary>
    [Export] public bool LoopPreview { get; set; } = true;

    internal void SetTrajectoryLane(int laneIndex, int laneCount, float groupDirection)
    {
        var side = laneIndex % 2 == 0 ? 1f : -1f;
        _curveDirection = Mathf.Sign(groupDirection) * side;
        _curveScaleMultiplier = laneCount <= 1
            ? 1f
            : 0.7f + laneIndex / 2f * 0.25f;
        _flutterPhase = laneCount <= 1
            ? 0f
            : Mathf.Tau * laneIndex / laneCount;
    }

    public void SetPath(Vector2 start, Vector2 end)
    {
        _startPos = start;
        _endPos = end;
        _hasPath = true;

        if (IsNodeReady())
            InitializePath();
    }

    /// <summary>
    /// 重置并重新播放一次飞行。游戏内由 _Ready 触发，预览时也可手动/循环触发。
    /// </summary>
    public void Replay()
    {
        if (!IsNodeReady() || _sprites == null)
            return;

        _tween?.Kill();
        _tween = null;

        IsFinished = false;
        _isHit = false;
        _travelProgress = 0f;

        _sprites.Modulate = _spriteBaseModulate;
        _sprites.Scale = _spriteBaseScale;

        RandomizeFlight();

        if (_hasPath)
        {
            InitializePath();
        }
        else if (VfxPreviewSupport.IsPreviewRun(this))
        {
            // 仅「F6 单独预览」且无外部路径时，走自动演示航程，避免飞在视口角落。
            var (start, end) = VfxPreviewSupport.PreviewTravelSpan(this);
            if (!AutoPreviewSpan)
            {
                start = _previewOrigin + PreviewStart;
                end = _previewOrigin + PreviewEnd;
            }

            SetPath(start, end);
        }
        // 游戏内若外部未提供路径，保持原行为：不移动，仅广播 Spawn。

        // 与原始实现一致：路径就绪后再广播 Spawn。
        EmitSpawnSignal();
    }

    private void InitializePath()
    {
        GlobalPosition = _startPos;

        _totalDistance = _startPos.DistanceTo(_endPos);
        if (_totalDistance > 0.001f)
        {
            IsFinished = false;
            var direction = (_endPos - _startPos).Normalized();
            var perpendicular = new Vector2(-direction.Y, direction.X);
            var curveOffset = Mathf.Min(CurveStrength * _curveScale, _totalDistance * MaxCurveDistanceRatio) *
                              _curveDirection;
            _controlPoint = (_startPos + _endPos) * 0.5f + perpendicular * curveOffset;
            _travelProgress = 0f;
            _isMoving = true;
        }
        else
        {
            _isMoving = false;
            _isHit = true;
            IsFinished = false;
            CallDeferred(nameof(OnReachedEndAsync));
        }
    }

    public override void _Ready()
    {
        _sprites = GetNode<Sprite2D>("Notes");
        _previewOrigin = GlobalPosition;
        _spriteBaseScale = _sprites.Scale;
        _spriteBaseModulate = _sprites.Modulate;

        if (VfxPreviewSupport.AutoPlayOnReady)
            Replay();
    }

    /// <summary>随机化精灵帧与飞行扰动，与游戏内原始随机化行为一致（不含信号广播）。</summary>
    private void RandomizeFlight()
    {
        if (_sprites == null)
            return;

        _sprites.Frame = Rng.Chaotic.NextInt(0, _sprites.Hframes * _sprites.Vframes - 1);
        var curveVariation = TrajectoryVariation * 0.35f;
        _curveScale = _curveScaleMultiplier *
                      (1f + Rng.Chaotic.NextFloat(-curveVariation, curveVariation));
        _flutterAmplitude = BaseFlutterAmplitude *
                            (1f + Rng.Chaotic.NextFloat(-TrajectoryVariation, TrajectoryVariation));
        _flutterFrequency = BaseFlutterFrequency *
                            (1f + Rng.Chaotic.NextFloat(-TrajectoryVariation, TrajectoryVariation));
        _flutterPhase += Rng.Chaotic.NextFloat(-TrajectoryVariation, TrajectoryVariation);
    }

    public override void _Process(double delta)
    {
        if (!_isMoving)
            return;

        if (!VfxPreviewSupport.ShouldContinuePlayback(this))
        {
            _isMoving = false;
            EmitFinishSignal();
            return;
        }

        _travelProgress = Mathf.Min(_travelProgress + (float)(Speed * delta) / _totalDistance, 1f);
        if (_travelProgress >= 1f)
        {
            _isMoving = false;
        }

        var easedProgress = SmoothStep(_travelProgress);
        var pathPosition = QuadraticBezier(_startPos, _controlPoint, _endPos, easedProgress);
        var tangent = QuadraticBezierDerivative(_startPos, _controlPoint, _endPos, easedProgress).Normalized();
        var normal = new Vector2(-tangent.Y, tangent.X);
        var flutterEnvelope = Mathf.Sin(Mathf.Pi * easedProgress);
        var flutter = Mathf.Sin(Mathf.Tau * _flutterFrequency * easedProgress + _flutterPhase) *
                      _flutterAmplitude * flutterEnvelope;

        GlobalPosition = pathPosition + normal * flutter;
        Rotation = tangent.Angle();

        if (!_isHit && _travelProgress >= 1f)
        {
            _isHit = true;
            _tween = CreateTween();
            if (_sprites != null)
            {
                _tween.TweenProperty(_sprites, "modulate:a", 0f, 0.25f);
                _tween.TweenProperty(_sprites, "scale", Vector2.Zero, 0.25f);
            }

            CallDeferred(nameof(OnReachedEndAsync));
        }
    }

    private static float SmoothStep(float value)
    {
        return value * value * (3f - 2f * value);
    }

    private static Vector2 QuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float progress)
    {
        var inverse = 1f - progress;
        return inverse * inverse * start + 2f * inverse * progress * control + progress * progress * end;
    }

    private static Vector2 QuadraticBezierDerivative(Vector2 start, Vector2 control, Vector2 end, float progress)
    {
        return 2f * (1f - progress) * (control - start) + 2f * progress * (end - control);
    }

    private async void OnReachedEndAsync()
    {
        try
        {
            EmitBeforeHitSignal();

            EmitHitSignal();

            EmitAfterHitSignal();

            if (_tween != null)
                await ToSignal(_tween, Tween.SignalName.Finished);
        }
        catch (Exception ex)
        {
            BangDreamLibCore.Logger.Warn($"MusicNoteVfx lifecycle exception: {ex}");
        }
        finally
        {
            var previewing = !VfxPreviewSupport.ShouldSelfFree(this);

            EmitFinishSignal();

            // 预览态不自毁，按需循环重播以便反复打磨。
            if (previewing && LoopPreview)
                Replay();
        }
    }

    /// <summary>仅在「F6 单独运行预览」中改动飞行参数时立即重播；编辑器里编辑场景不重播。</summary>
    private void ReplayIfPreviewing()
    {
        if (IsNodeReady() && VfxPreviewSupport.IsPreviewRun(this))
            Replay();
    }
}
