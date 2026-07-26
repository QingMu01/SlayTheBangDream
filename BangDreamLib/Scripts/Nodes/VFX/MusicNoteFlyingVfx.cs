using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Random;

namespace BangDreamLib.Scripts.Nodes.VFX;

public partial class MusicNoteFlyingVfx : NBangDreamFlyingVfx
{
    private const float BaseFlutterAmplitude = 10f;
    private const float BaseFlutterFrequency = 1.6f;
    private const float MaxCurveDistanceRatio = 0.45f;

    [Export] public double Speed = 800.0f;

    [Export(PropertyHint.Range, "0,160,1")]
    public float CurveStrength { get; set; } = 128f;

    [Export(PropertyHint.Range, "0,0.8,0.01")]
    public float TrajectoryVariation { get; set; } = 0.7f;

    private float _curveDirection = -1f;
    private float _curveScaleMultiplier = 1f;
    private float _curveScale;
    private float _flutterAmplitude;
    private float _flutterFrequency;
    private float _flutterPhase;

    private Sprite2D? _sprites;
    private Tween? _tween;

    private Vector2 _startPos;
    private Vector2 _endPos;
    private Vector2 _controlPoint;
    private float _totalDistance;
    private float _travelProgress;
    private bool _hasPath;
    private bool _isMoving;
    private bool _isHit;

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
        _sprites.Frame = Rng.Chaotic.NextInt(0, _sprites.Hframes * _sprites.Vframes - 1);
        var curveVariation = TrajectoryVariation * 0.35f;
        _curveScale = _curveScaleMultiplier *
                      (1f + Rng.Chaotic.NextFloat(-curveVariation, curveVariation));
        _flutterAmplitude = BaseFlutterAmplitude *
                            (1f + Rng.Chaotic.NextFloat(-TrajectoryVariation, TrajectoryVariation));
        _flutterFrequency = BaseFlutterFrequency *
                            (1f + Rng.Chaotic.NextFloat(-TrajectoryVariation, TrajectoryVariation));
        _flutterPhase += Rng.Chaotic.NextFloat(-TrajectoryVariation, TrajectoryVariation);

        if (_hasPath)
            InitializePath();

        EmitSpawnSignal();
    }

    public override void _Process(double delta)
    {
        if (!_isMoving)
            return;

        if (!CombatManager.Instance.IsInProgress)
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
            _tween.TweenProperty(_sprites, "modulate:a", 0f, 0.25f);
            _tween.TweenProperty(_sprites, "scale", Vector2.Zero, 0.25f);
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
            EmitFinishSignal();
        }
    }
}