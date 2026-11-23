using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace BangDreamInSpire.Scripts.Mechanics.VisualsAnim;

/// <summary>以 Holder → Clone → Mirror 代理原体视觉，分离位移与局部姿态，
/// 不影响玩法节点；Mirror 同步本体骨架当前帧。</summary>
public sealed class VisualsAnimProxy
{
    private VisualsAnimProxy(
        Creature creature,
        NCreature source,
        Node2D holder,
        NCreatureVisuals visuals,
        VisualsAnimMirror? mirror,
        Vector2 home,
        Vector2 baseScale)
    {
        Creature = creature;
        Source = source;
        Holder = holder;
        Visuals = visuals;
        Mirror = mirror;
        Home = home;
        BaseScale = baseScale;
        _facing = baseScale.X >= 0f ? 1f : -1f;
    }

    public Creature Creature { get; }

    public NCreature Source { get; }

    public Node2D Holder { get; }

    public NCreatureVisuals Visuals { get; }

    /// <summary>骨架镜像驱动，非骨架为 null。</summary>
    public VisualsAnimMirror? Mirror { get; }

    /// <summary>原体基准位置，用于归位。</summary>
    public Vector2 Home { get; }

    /// <summary>原体基准缩放。</summary>
    public Vector2 BaseScale { get; }

    public bool IsAlive =>
        GodotObject.IsInstanceValid(Source) && GodotObject.IsInstanceValid(Holder);

    public bool IsActive { get; private set; }

    /// <summary>朝向符号，+1 右、-1 左。</summary>
    private float _facing = 1f;

    private float _spin;

    public Vector2 Position
    {
        get => Holder.Position;
        set => Holder.Position = value;
    }

    public Vector2 GlobalPosition => Holder.GlobalPosition;

    /// <summary>演出期间的命中特效落点。</summary>
    public Vector2 VfxSpawnPosition => GodotObject.IsInstanceValid(Visuals)
        ? Visuals.VfxSpawnPosition.GlobalPosition
        : Source.VfxSpawnPosition;

    /// <summary>以基准缩放设置朝向。</summary>
    public void Face(bool right)
    {
        _facing = right ? 1f : -1f;
        ApplyTransform();
    }

    /// <summary>用横向缩放的 cos 模拟绕纵轴自转。</summary>
    public void SetSpin(float radians)
    {
        _spin = radians;
        ApplyTransform();
    }

    private void ApplyTransform()
    {
        if (!IsAlive)
            return;

        Visuals.Scale = new Vector2(
            Mathf.Abs(BaseScale.X) * Mathf.Cos(_spin) * _facing,
            BaseScale.Y);
    }

    public void PlayShake()
    {
        Mirror?.StartShake();
    }

    /// <summary>恢复基准位置与朝向。</summary>
    public void Reset()
    {
        if (!IsAlive)
            return;

        Holder.Position = Home;
        Visuals.Position = Vector2.Zero;
        _facing = BaseScale.X >= 0f ? 1f : -1f;
        _spin = 0f;
        ApplyTransform();
        Mirror?.ResetState();
    }

    internal void Begin()
    {
        if (!IsAlive)
            return;

        Reset();
        Visuals.Visible = true;
        Source.Visuals.Visible = false;
        IsActive = true;

        if (Mirror != null)
            Mirror.Active = true;
    }

    internal void End()
    {
        if (IsAlive)
        {
            if (Mirror != null)
            {
                Mirror.Active = false;
                // 清除残留抖动相位。
                Mirror.ResetState();
            }

            Visuals.Visible = false;
            Source.Visuals.Visible = true;
        }

        IsActive = false;
    }

    /// <summary>恢复原体并销毁代理节点。</summary>
    internal void Restore()
    {
        IsActive = false;

        if (GodotObject.IsInstanceValid(Source))
            Source.Visuals.Visible = true;

        if (GodotObject.IsInstanceValid(Holder))
            Holder.QueueFreeSafely();
    }

    internal static VisualsAnimProxy? TryCreate(Creature creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        var source = NCombatRoom.Instance?.GetCreatureNode(creature);
        if (source == null || !GodotObject.IsInstanceValid(source.Visuals))
            return null;

        // 无渲染环境仅执行逻辑步骤。
        var clone = creature.CreateVisuals();
        if (clone == null)
            return null;

        var home = source.Visuals.Position;
        var baseScale = source.Visuals.Scale;

        var holder = new Node2D { Name = "VisualsAnimHolder" };
        source.AddChildSafely(holder);
        holder.Position = home;

        // 载体置前，避免克隆遮挡血条与意图。
        source.MoveChildSafely(holder, 0);

        holder.AddChildSafely(clone);
        clone.Position = Vector2.Zero;
        clone.Scale = baseScale;
        clone.Visible = false;

        // 克隆需按原体顺序先 SetUpSkin 再设置动画；玩家角色无需此步骤。
        // 不复现 phobia 体型，镜像仅驱动 SpineBody。
        if (creature.Monster != null && clone.HasSpineAnimation)
            clone.SetUpSkin(creature.Monster);

        // 克隆同步本体当前帧。
        VisualsAnimMirror? mirror = null;
        if (source.HasSpineAnimation && clone.HasSpineAnimation)
        {
            mirror = new VisualsAnimMirror { Name = "VisualsAnimMirror" };
            clone.AddChildSafely(mirror);
            mirror.Bind(source, clone);
        }

        return new VisualsAnimProxy(creature, source, holder, clone, mirror, home, baseScale);
    }
}
