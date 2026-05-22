using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Random;

namespace BangDreamLib.Scripts.Nodes.VFX;

public partial class MusicNoteVfx : Node2D
{
    private const string DefaultPath = "res://ItsCrychic/scenes/vfx/flying_music_note_default.tscn";

    [Export] public float Speed = 800.0f;
    [Export] public float FloatAmplitude = 20.0f;
    [Export] public int FloatFrequency = 2;
    [Export] public float FloatDirection = -1.0f;

    private Sprite2D _sprites;

    private Vector2 _startPos;
    private Vector2 _endPos;
    private Vector2 _direction;
    private float _totalDistance;
    private float _traveledDistance;
    private bool _isMoving;
    private bool _hasReachedEnd;

    private int _actualFrequency;
    private float _amplitudeModifier;

    private Func<MusicNoteVfx, Task>? _onReachedEndAsync;

    public static MusicNoteVfx Create(Vector2 start, Vector2 end, string? specialPath = null,
        Func<MusicNoteVfx, Task>? onReachedEndAsync = null)
    {
        var instance = PreloadManager.Cache.GetScene(specialPath ?? DefaultPath).Instantiate<MusicNoteVfx>();
        instance._startPos = start;
        instance._endPos = end;
        instance.Position = start;
        instance._onReachedEndAsync = onReachedEndAsync;

        return instance;
    }

    public override void _Ready()
    {
        if (NCombatUi.IsDebugHidingPlayContainer)
            Visible = false;

        _sprites = GetNode<Sprite2D>("Notes");

        _sprites.Frame = Rng.Chaotic.NextInt(0, _sprites.Hframes * _sprites.Vframes - 1);

        _direction = (_endPos - _startPos).Normalized();
        _totalDistance = _startPos.DistanceTo(_endPos);
        _traveledDistance = 0f;
        _isMoving = true;
    }

    public override void _Process(double delta)
    {
        if (!_isMoving) return;
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
        var offsetMagnitude = FloatAmplitude * Mathf.Sin(t * FloatFrequency * Mathf.Tau / _totalDistance);

        var floatOffset = perpendicular * offsetMagnitude * FloatDirection;

        GlobalPosition = linearPos + floatOffset;
        Rotation = _direction.Angle();
        if (!_hasReachedEnd && _traveledDistance >= _totalDistance)
        {
            _hasReachedEnd = true;
            _ = HandleReachEndAsync();
        }
    }

    private async Task HandleReachEndAsync()
    {
        try
        {
            if (_onReachedEndAsync != null)
            {
                await _onReachedEndAsync(this);
            }

            await Task.Delay(500);
        }
        catch (Exception ex)
        {
            BangDreamLibCore.Logger.Warn($"music note vfx callback has beed exception: {ex}");
        }
        finally
        {
            QueueFree();
        }
    }
}