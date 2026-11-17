using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace BangDreamInSpire.Scripts.Mechanics.VisualsAnim;

/// <summary>
/// 角色演出的视觉代理。
/// <para>
/// 演出期间隐藏原体 <see cref="NCreatureVisuals"/>，把 <see cref="Creature.CreateVisuals" /> 克隆出来的副本挂在同一个
/// <see cref="NCreature" /> 下，由演出时间线自由驱动位移与朝向。玩法状态、命中框、血条、意图都不受影响
/// ——它们都是 <see cref="NCreature" /> 的直接子节点，不是 <see cref="NCreatureVisuals" /> 的后代。
/// </para>
/// <para>
/// 节点结构：<c>NCreature → Holder(Node2D) → Clone(NCreatureVisuals) → Mirror(Node)</c>。
/// 位移写 <c>Holder.Position</c>、抖动与其它姿态写 <c>Clone</c> 的局部变换，二者互不覆盖；
/// <see cref="VisualsAnimMirror" /> 负责让克隆骨架跟随本体骨架的当前帧。
/// </para>
/// <para>
/// 这是 STS1 <c>AbstractRenderer</c> 偏移通道的对位物：STS1 的偏移只作用于精灵绘制，血条本来就留在布局坐标上，
/// 这里的表现完全一致。
/// </para>
/// </summary>
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

    /// <summary>移动载体：位移只写它的位置。</summary>
    public Node2D Holder { get; }

    public NCreatureVisuals Visuals { get; }

    /// <summary>骨架镜像驱动器；非骨架生物为 null。</summary>
    public VisualsAnimMirror? Mirror { get; }

    /// <summary>原体 <see cref="NCreatureVisuals" /> 在 <see cref="NCreature" /> 坐标系下的基准位置；归位时回到这里。</summary>
    public Vector2 Home { get; }

    /// <summary>原体视觉的基准缩放；翻转换向时以它的绝对值作为基准。</summary>
    public Vector2 BaseScale { get; }

    /// <summary>代理是否仍然有效（本体节点或载体被释放即为失效）。</summary>
    public bool IsAlive =>
        GodotObject.IsInstanceValid(Source) && GodotObject.IsInstanceValid(Holder);

    /// <summary>代理当前是否在演出（原体被隐藏、克隆可见）。</summary>
    public bool IsActive { get; private set; }

    /// <summary>朝向符号：+1 面向右，-1 面向左。</summary>
    private float _facing = 1f;

    /// <summary>当前自转弧度。</summary>
    private float _spin;

    /// <summary>载体的位置（<see cref="NCreature" /> 坐标系）。</summary>
    public Vector2 Position
    {
        get => Holder.Position;
        set => Holder.Position = value;
    }

    public Vector2 GlobalPosition => Holder.GlobalPosition;

    /// <summary>命中特效的落点。演出期间 <see cref="NCreature.VfxSpawnPosition" /> 会被重定向到这里。</summary>
    public Vector2 VfxSpawnPosition => Visuals.VfxSpawnPosition.GlobalPosition;

    /// <summary>把世界坐标换算成载体的局部位置，供「移动到某个生物身边」这类步骤使用。</summary>
    public Vector2 ToLocal(Vector2 global)
    {
        return Source.GetGlobalTransform().AffineInverse() * global;
    }

    /// <summary>朝向：以 <see cref="BaseScale" /> 的绝对值取符号，避免与外部缩放叠加出错。</summary>
    public void Face(bool right)
    {
        _facing = right ? 1f : -1f;
        ApplyTransform();
    }

    /// <summary>
    /// 绕角色纵轴自转，弧度制。STS1 <c>FlipProcess</c> 的 <c>rotateY</c> 在 2D 里的等价实现：
    /// 横向缩放取 <c>cos</c>，转到 90° 时侧面缩为 0、越过 180° 后即成背面镜像。
    /// </summary>
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

    /// <summary>受击抖动（转发自本体 <c>AnimShake</c>）。</summary>
    public void PlayShake()
    {
        Mirror?.StartShake();
    }

    /// <summary>回到基准位置与基准朝向。</summary>
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
                Mirror.Active = false;

            Visuals.Visible = false;
            Source.Visuals.Visible = true;
        }

        IsActive = false;
    }

    /// <summary>彻底释放：恢复原体可见并销毁载体（连同克隆与镜像）。战斗结束时调用。</summary>
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

        // 无渲染 / 测试环境返回 null，演出退化为「只跑逻辑步骤」。
        var clone = creature.CreateVisuals();
        if (clone == null)
            return null;

        var home = source.Visuals.Position;
        var baseScale = source.Visuals.Scale;

        var holder = new Node2D { Name = "VisualsAnimHolder" };
        source.AddChildSafely(holder);
        holder.Position = home;

        // 与原体 Visuals 一样放到子节点最前：血条 / 意图 / 命中框是后加的直接子节点，
        // 载体若不前置，克隆会盖在它们上面。
        source.MoveChildSafely(holder, 0);

        holder.AddChildSafely(clone);
        clone.Position = Vector2.Zero;
        clone.Scale = baseScale;
        clone.Visible = false;

        // 骨架克隆必须复现本体的当前帧，否则动画只会停在待机。
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
