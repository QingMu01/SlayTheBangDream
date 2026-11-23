using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace BangDreamInSpire.Scripts.Mechanics.VisualsAnim;

/// <summary>逐帧同步本体骨架与克隆，并将受击抖动写入克隆局部坐标。</summary>
public partial class VisualsAnimMirror : Node
{
    private const float ShakeAmplitude = 10f;
    private const float ShakeDuration = 1f;

    private NCreature? _source;
    private NCreatureVisuals? _clone;
    private string? _lastAnimation;
    private float _shakeElapsed = -1f;

    /// <summary>仅演出时推进。</summary>
    internal bool Active
    {
        get => IsProcessing();
        set => SetProcess(value);
    }

    internal void Bind(NCreature source, NCreatureVisuals clone)
    {
        _source = source;
        _clone = clone;
        SetProcess(false);
    }

    internal void StartShake()
    {
        // 已在抖动或受击动画中时不重开。
        if (_shakeElapsed >= 0f)
            return;

        if (GodotObject.IsInstanceValid(_source) && _source!.Visuals.IsPlayingHurtAnimation())
            return;

        _shakeElapsed = 0f;
    }

    /// <summary>重置同步状态。</summary>
    internal void ResetState()
    {
        _lastAnimation = null;
        _shakeElapsed = -1f;

        if (GodotObject.IsInstanceValid(_clone))
            _clone!.Position = Vector2.Zero;
    }

    public override void _Process(double delta)
    {
        if (!GodotObject.IsInstanceValid(_source) || !GodotObject.IsInstanceValid(_clone))
        {
            SetProcess(false);
            return;
        }

        MirrorAnimation();
        MirrorShake((float)delta);
    }

    /// <summary>同步动画与轨道时间。</summary>
    private void MirrorAnimation()
    {
        var sourceAnim = _source!.SpineAnimation;
        var cloneAnim = _clone!.SpineAnimation;
        if (!sourceAnim.IsValid || !cloneAnim.IsValid)
            return;

        var name = sourceAnim.GetCurrentAnimationName();
        if (string.IsNullOrEmpty(name))
            return;

        using var sourceTrack = sourceAnim.GetCurrentTrack();
        if (sourceTrack == null)
            return;

        if (!string.Equals(name, _lastAnimation, StringComparison.Ordinal))
        {
            cloneAnim.SetAnimation(name, sourceTrack.IsLoop());
            _lastAnimation = name;
        }

        using var cloneTrack = cloneAnim.GetCurrentTrack();
        cloneTrack?.SetTrackTime(sourceTrack.GetTrackTime());
    }

    /// <summary>复刻受击抖动曲线。</summary>
    private void MirrorShake(float delta)
    {
        if (_shakeElapsed < 0f)
            return;

        _shakeElapsed += delta;
        var progress = Mathf.Min(_shakeElapsed / ShakeDuration, 1f);
        var eased = 1f - Mathf.Pow(1f - progress, 3f);
        var angle = Mathf.Tau * eased;

        _clone!.Position = new Vector2(
            ShakeAmplitude * Mathf.Sin(angle * 4f) * Mathf.Sin(angle * 0.5f),
            0f);

        if (progress >= 1f)
        {
            _shakeElapsed = -1f;
            _clone.Position = Vector2.Zero;
        }
    }
}
