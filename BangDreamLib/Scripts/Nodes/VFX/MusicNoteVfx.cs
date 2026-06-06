using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Random;

namespace BangDreamLib.Scripts.Nodes.VFX;

public partial class MusicNoteVfx : NBangDreamVfx
{
    [Export] public float Speed = 800.0f;

    private int _floatFrequency = 2;
    private float _floatAmplitude = 20.0f;
    private float _floatDirection = -1.0f;

    private Sprite2D? _sprites;
    private Tween? _tween;

    private Vector2 _startPos;
    private Vector2 _endPos;
    private Vector2 _direction;
    private float _totalDistance;
    private float _traveledDistance;
    private bool _isMoving;
    private bool _isHit;

    public void SetPath(Vector2 start, Vector2 end)
    {
        _startPos = start;
        _endPos = end;
        GlobalPosition = start;

        _totalDistance = _startPos.DistanceTo(_endPos);
        if (_totalDistance > 0.001f)
        {
            IsFinished = false;
            _direction = (_endPos - _startPos).Normalized();
            _traveledDistance = 0f;
            _isMoving = true;
        }
        else
        {
            _isMoving = false;
            IsFinished = true;
            _ = OnReachedEndAsync();
        }
    }

    public override void _Ready()
    {
        _sprites = GetNode<Sprite2D>("Notes");
        _sprites.Frame = Rng.Chaotic.NextInt(0, _sprites.Hframes * _sprites.Vframes - 1);

        EmitSpawnSignal();
    }

    public override void _Process(double delta)
    {
        if (!_isMoving || !CombatManager.Instance.IsInProgress)
            return;

        var step = Speed * (float)delta;
        _traveledDistance += step;

        if (_traveledDistance >= _totalDistance)
        {
            _traveledDistance = _totalDistance;
            _isMoving = false;
        }

        var linearPos = _startPos + _direction * _traveledDistance;
        var perpendicular = new Vector2(-_direction.Y, _direction.X);
        var t = _traveledDistance;
        var offsetMagnitude = _floatAmplitude * Mathf.Sin(t * _floatFrequency * Mathf.Tau / _totalDistance);
        var floatOffset = perpendicular * offsetMagnitude * _floatDirection;

        GlobalPosition = linearPos + floatOffset;
        Rotation = _direction.Angle();

        if (!_isHit && _traveledDistance >= _totalDistance)
        {
            _isHit = true;
            _tween = CreateTween();
            _tween.TweenProperty(_sprites, "modulate:a", 0f, 0.25f);
            _tween.TweenProperty(_sprites, "scale", Vector2.Zero, 0.25f);
            _ = OnReachedEndAsync();
        }
    }

    private async Task OnReachedEndAsync()
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