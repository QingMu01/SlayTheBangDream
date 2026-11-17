using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace BangDreamInSpire.Scripts.Mechanics.VisualsAnim;

/// <summary>
/// 让克隆骨架**跟随本体骨架当前帧**的驱动器。
/// <para>
/// 本体的 <c>CreatureAnimator</c> 绑定在 <see cref="NCreature.Visuals" /> 的骨架上（<c>NCreature._spineAnimator</c>），
/// 而 <c>SetAnimationTrigger</c> 只转发给它。演出期间原体被隐藏但仍在推进，
/// 所以只要逐帧把它的动画名、循环标记与轨道时间镜像到克隆，攻击 / 施法 / 受击 / 死亡 / 待机全部自然同步，
/// 不需要再造一套状态机。
/// </para>
/// <para>
/// 同时负责受击抖动：本体的 <c>AnimShake</c> 写的是 <c>Visuals.Position</c>（隐藏中不可见），
/// 这里改为写在克隆自己的局部位置——克隆位于移动载体之下，故抖动与位移互不覆盖。
/// </para>
/// </summary>
public partial class VisualsAnimMirror : Node
{
    private const float ShakeAmplitude = 10f;
    private const float ShakeDuration = 1f;

    private NCreature? _source;
    private NCreatureVisuals? _clone;
    private string? _lastAnimation;
    private float _shakeElapsed = -1f;

    /// <summary>仅当代理处于演出态时推进镜像；其余时间不耗帧。</summary>
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
        // 与原体 AnimShake 的守卫一致：正在抖动或正在播受击动画时不重开。
        if (_shakeElapsed >= 0f)
            return;

        if (GodotObject.IsInstanceValid(_source) && _source!.Visuals.IsPlayingHurtAnimation())
            return;

        _shakeElapsed = 0f;
    }

    /// <summary>重新开始时清掉同步状态，让下一次推进重新对齐本体。</summary>
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

    /// <summary>把本体当前轨道状态镜像到克隆。动画切换时才重设动画，其余帧只对齐轨道时间。</summary>
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

    /// <summary>复刻本体的受击抖动曲线（横向 10px、1 秒、Cubic 缓出）。</summary>
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
