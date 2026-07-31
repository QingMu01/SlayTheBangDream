using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Random;

namespace BangDreamLib.Scripts.Nodes.VFX;

public sealed partial class NLingeredOrbitVfx : Node2D
{
    public const string AttachmentId = "lingered_orbit_vfx";
    public const string AttachmentName = "LingeredOrbitVfx";

    private const float OrbitAngularSpeed = 0.72f;
    private const float LayoutFollowSpeed = 8f;
    private const float NoteBaseScale = 0.5f;
    private const float GatherDuration = 0.34f;
    private const float ExplosionDuration = 0.28f;
    private const float ExplosionHoldDuration = 0.045f;
    private const float NoteFadeInDuration = 0.1f;
    private const float ExplosionFadeOutDuration = 0.2f;
    private const float PreviewRiseSpeed = 10f;
    private const float PreviewReturnSpeed = 16f;
    private const float PreviewCleanupDelay = 0.75f;

    private readonly List<OrbitNote> _notes = [];
    private readonly Queue<ResourceVisualChange> _pendingChanges = [];
    private readonly List<OrbitNote> _previewNotes = [];

    private NCreature? _creature;
    private Node2D? _backLayer;
    private Node2D? _frontLayer;
    private Node2D? _effectLayer;
    private Sprite2D? _noteTemplate;
    private Sprite2D? _particleTemplate;
    private GatherBatch? _gatherBatch;
    private ExplosionBatch? _explosionBatch;
    private ResourceVisualChange? _activeChange;
    private int? _pendingInitialAmount;
    private int _previewRevision;
    private float _previewCleanupRemaining = -1f;
    private float _orbitPhase;
    private Vector2 _orbitRadius = new(112f, 38f);
    private float _previewHeight = 138f;
    private bool _initialized;
    private bool _isExiting;

    public static NLingeredOrbitVfx Create(NCreature creature)
    {
        var vfx = PreloadKey.LingeredOrbitVfx.GetScene().Instantiate<NLingeredOrbitVfx>();
        vfx._creature = creature;
        return vfx;
    }

    public override void _Ready()
    {
        _backLayer = GetNode<Node2D>("%Back");
        _frontLayer = GetNode<Node2D>("%Front");
        _effectLayer = GetNode<Node2D>("%Effects");
        _noteTemplate = GetNode<Sprite2D>("%NoteTemplate");
        _particleTemplate = GetNode<Sprite2D>("%ParticleTemplate");
        _noteTemplate.Visible = false;

        if (_creature != null)
        {
            UpdateCreatureBoundsMetrics();
            GlobalPosition = _creature.VfxSpawnPosition;
            AttachVisualLayersToCreature();
        }

        SetProcess(false);
        if (_pendingInitialAmount is { } initialAmount)
        {
            Initialize(initialAmount);
        }
    }

    public override void _ExitTree()
    {
        _isExiting = true;
        _pendingChanges.Clear();
        _previewNotes.Clear();
        _activeChange = null;
        _gatherBatch = null;
        _explosionBatch = null;
        QueueFreeDetachedLayer(_backLayer);
        QueueFreeDetachedLayer(_frontLayer);
        QueueFreeDetachedLayer(_effectLayer);
        _backLayer = null;
        _frontLayer = null;
        _effectLayer = null;
    }

    public override void _Process(double delta)
    {
        if (!_initialized || _isExiting ||
            _backLayer == null || _frontLayer == null || _effectLayer == null)
        {
            return;
        }

        var frameDelta = (float)delta;
        SyncLayersToCreature();
        _orbitPhase = Mathf.PosMod(_orbitPhase + OrbitAngularSpeed * frameDelta, Mathf.Tau);

        UpdatePreviewCleanup(frameDelta);
        UpdateNotes(frameDelta);

        if (_gatherBatch != null)
        {
            UpdateGatherBatch(frameDelta);
        }
        else if (_explosionBatch != null)
        {
            UpdateExplosionBatch(frameDelta);
        }
        else
        {
            StartNextResourceChange();
        }
    }

    public void Initialize(int initialAmount)
    {
        if (!IsNodeReady())
        {
            _pendingInitialAmount = initialAmount;
            return;
        }

        _pendingInitialAmount = null;
        _isExiting = false;
        _initialized = true;
        _previewRevision = 0;
        _previewCleanupRemaining = -1f;
        _pendingChanges.Clear();
        _activeChange = null;
        _gatherBatch = null;
        _explosionBatch = null;
        UpdateCreatureBoundsMetrics();
        SyncLayersToCreature();
        ClearEffectLayer();
        SetAmountImmediately(Math.Max(0, initialAmount));
        SetProcess(true);
    }

    public void Shutdown()
    {
        _initialized = false;
        SetProcess(false);
        _pendingChanges.Clear();
        _activeChange = null;
        _gatherBatch = null;
        _explosionBatch = null;
        _previewNotes.Clear();
        _previewCleanupRemaining = -1f;
        ClearAllNotes();
        ClearEffectLayer();
    }

    public void SubmitResourceAmount(int targetAmount, int previewAmountAfter)
    {
        if (!_initialized)
        {
            return;
        }

        _pendingChanges.Enqueue(new ResourceVisualChange(
            Math.Max(0, targetAmount),
            Math.Max(0, previewAmountAfter),
            _previewRevision));
    }

    public void ShowPreview(int amount)
    {
        _previewRevision++;
        _previewCleanupRemaining = -1f;
        ApplyPreviewAmount(Math.Max(0, amount));
    }

    public void CancelPreview()
    {
        _previewRevision++;
        ClearPreview();
    }

    public void SchedulePreviewCleanup()
    {
        if (_previewNotes.Count > 0)
        {
            _previewCleanupRemaining = PreviewCleanupDelay;
        }
    }

    private void StartNextResourceChange()
    {
        if (_pendingChanges.Count == 0)
        {
            return;
        }

        _activeChange = _pendingChanges.Dequeue();
        if (_activeChange.TargetAmount > _notes.Count)
        {
            StartGather(_activeChange.TargetAmount - _notes.Count);
            return;
        }

        if (_activeChange.TargetAmount < _notes.Count)
        {
            StartExplosion(_notes.Count - _activeChange.TargetAmount);
            return;
        }

        CompleteActiveResourceChange();
    }

    private void StartGather(int amount)
    {
        if (_effectLayer == null)
        {
            SetAmountImmediately(_activeChange!.TargetAmount);
            CompleteActiveResourceChange();
            return;
        }

        var newNotes = new List<OrbitNote>(amount);
        for (var i = 0; i < amount; i++)
        {
            var note = CreateNote(visible: false);
            _notes.Add(note);
            newNotes.Add(note);
        }

        AssignEvenSlots();

        var particles = new List<GatherParticle>(amount * 6);
        foreach (var note in newNotes)
        {
            var predictedAngle = _orbitPhase + note.TargetSlotOffset + OrbitAngularSpeed * GatherDuration;
            var endpoint = GetOrbitPosition(predictedAngle);
            for (var particleIndex = 0; particleIndex < 6; particleIndex++)
            {
                var sourceAngle = Rng.Chaotic.NextFloat(0f, Mathf.Tau);
                var sourceRadius = Rng.Chaotic.NextFloat(58f, 125f);
                var start = new Vector2(
                    Mathf.Cos(sourceAngle) * sourceRadius,
                    Mathf.Sin(sourceAngle) * sourceRadius * 0.62f);
                var direction = (endpoint - start).Normalized();
                var perpendicular = new Vector2(-direction.Y, direction.X);
                var control = (start + endpoint) * 0.5f +
                              perpendicular * Rng.Chaotic.NextFloat(-48f, 48f);
                var particleScale = Rng.Chaotic.NextFloat(0.012f, 0.032f);
                var sprite = CreateParticleSprite(particleScale);
                sprite.Position = start;
                _effectLayer.AddChild(sprite);
                particles.Add(new GatherParticle(
                    sprite,
                    note,
                    start,
                    control,
                    particleScale,
                    Rng.Chaotic.NextFloat(0f, 0.055f)));
            }
        }

        _gatherBatch = new GatherBatch(newNotes, particles);
    }

    private void UpdateGatherBatch(float delta)
    {
        var batch = _gatherBatch!;
        batch.Elapsed += delta;

        foreach (var particle in batch.Particles)
        {
            var progress = Mathf.Clamp(
                (batch.Elapsed - particle.Delay) / (GatherDuration - particle.Delay),
                0f,
                1f);
            var easedProgress = SmoothStep(progress);
            var remaining = Mathf.Max(0f, GatherDuration - batch.Elapsed);
            var predictedEnd = GetOrbitPosition(
                _orbitPhase +
                particle.TargetNote.TargetSlotOffset +
                OrbitAngularSpeed * remaining);
            particle.Sprite.Position = QuadraticBezier(
                particle.Start,
                particle.Control,
                predictedEnd,
                easedProgress);
            particle.Sprite.Modulate = new Color(1f, 1f, 1f, Mathf.Lerp(0.9f, 0.2f, progress));
            particle.Sprite.Scale = Vector2.One *
                                    Mathf.Lerp(particle.StartScale, particle.StartScale * 0.3f, easedProgress);
        }

        if (batch.Elapsed < GatherDuration)
        {
            return;
        }

        foreach (var particle in batch.Particles)
        {
            particle.Sprite.QueueFree();
        }

        foreach (var note in batch.NewNotes)
        {
            note.Sprite.Visible = true;
            note.Sprite.Modulate = new Color(
                note.Sprite.Modulate.R,
                note.Sprite.Modulate.G,
                note.Sprite.Modulate.B,
                0f);
            note.PopElapsed = 0f;
            note.FadeInElapsed = 0f;
        }

        _gatherBatch = null;
        CompleteActiveResourceChange();
    }

    private void StartExplosion(int amount)
    {
        if (_effectLayer == null)
        {
            SetAmountImmediately(_activeChange!.TargetAmount);
            CompleteActiveResourceChange();
            return;
        }

        var selected = SelectNotesForRemoval(amount);
        var bursts = new List<ExplosionBurst>(selected.Count);
        foreach (var note in selected)
        {
            var position = note.Sprite.Position;
            var burstSprite = CreateNoteSprite();
            burstSprite.Frame = note.Sprite.Frame;
            burstSprite.Position = position;
            burstSprite.Scale = note.Sprite.Scale;
            var burstModulate = note.Sprite.Modulate;
            burstModulate.A = Math.Max(0.85f, burstModulate.A);
            burstSprite.Modulate = burstModulate;
            _effectLayer.AddChild(burstSprite);

            const float flashStartScale = 0.035f;
            var flash = CreateParticleSprite(flashStartScale);
            flash.Position = position;
            flash.Modulate = new Color(0.72f, 0.9f, 1f, 0.9f);
            _effectLayer.AddChild(flash);

            var particles = new List<ExplosionParticle>(10);
            for (var particleIndex = 0; particleIndex < 10; particleIndex++)
            {
                var angle = Mathf.Tau * particleIndex / 10f + Rng.Chaotic.NextFloat(-0.16f, 0.16f);
                var direction = Vector2.FromAngle(angle);
                var particleScale = Rng.Chaotic.NextFloat(0.018f, 0.042f);
                var particle = CreateParticleSprite(particleScale);
                particle.Position = position;
                particle.Modulate = new Color(0.78f, 0.92f, 1f);
                _effectLayer.AddChild(particle);
                particles.Add(new ExplosionParticle(
                    particle,
                    position,
                    direction * Rng.Chaotic.NextFloat(135f, 225f),
                    particleScale));
            }

            bursts.Add(new ExplosionBurst(
                burstSprite,
                flash,
                burstSprite.Scale,
                burstSprite.Modulate,
                particles));
            _notes.Remove(note);
            _previewNotes.Remove(note);
            note.Sprite.QueueFree();
        }

        AssignEvenSlots();
        _explosionBatch = new ExplosionBatch(bursts);
    }

    private void UpdateExplosionBatch(float delta)
    {
        var batch = _explosionBatch!;
        batch.Elapsed += delta;
        var progress = Mathf.Clamp(batch.Elapsed / ExplosionDuration, 0f, 1f);
        var easedProgress = EaseOutCubic(progress);
        var fadeProgress = SmoothStep(Mathf.Clamp(
            (batch.Elapsed - ExplosionHoldDuration) / ExplosionFadeOutDuration,
            0f,
            1f));
        var flashProgress = SmoothStep(Mathf.Clamp(batch.Elapsed / 0.1f, 0f, 1f));

        foreach (var burst in batch.Bursts)
        {
            burst.Note.Scale = burst.StartScale * Mathf.Lerp(1f, 1.38f, easedProgress);
            var brightness = Mathf.Lerp(1f, 1.3f, 1f - flashProgress);
            burst.Note.Modulate = new Color(
                Math.Min(1f, burst.StartModulate.R * brightness),
                Math.Min(1f, burst.StartModulate.G * brightness),
                Math.Min(1f, burst.StartModulate.B * brightness),
                burst.StartModulate.A * (1f - fadeProgress));
            burst.Flash.Scale = Vector2.One * Mathf.Lerp(0.035f, 0.11f, easedProgress);
            burst.Flash.Modulate = new Color(
                0.72f,
                0.9f,
                1f,
                0.9f * (1f - fadeProgress));
            foreach (var particle in burst.Particles)
            {
                particle.Sprite.Position = particle.Start + particle.Velocity *
                    (ExplosionDuration * easedProgress);
                particle.Sprite.Modulate = new Color(0.78f, 0.92f, 1f, 1f - fadeProgress);
                var particlePulse = Mathf.Sin(progress * Mathf.Pi);
                particle.Sprite.Scale = Vector2.One *
                                        (particle.StartScale * Mathf.Lerp(0.8f, 1.35f, particlePulse));
            }
        }

        if (batch.Elapsed < ExplosionDuration)
        {
            return;
        }

        foreach (var burst in batch.Bursts)
        {
            burst.Note.QueueFree();
            burst.Flash.QueueFree();
            foreach (var particle in burst.Particles)
            {
                particle.Sprite.QueueFree();
            }
        }

        _explosionBatch = null;
        CompleteActiveResourceChange();
    }

    private List<OrbitNote> SelectNotesForRemoval(int amount)
    {
        var selected = new List<OrbitNote>(amount);
        foreach (var previewNote in _previewNotes)
        {
            if (_notes.Contains(previewNote) && !selected.Contains(previewNote))
            {
                selected.Add(previewNote);
                if (selected.Count == amount)
                {
                    return selected;
                }
            }
        }

        for (var index = _notes.Count - 1; index >= 0 && selected.Count < amount; index--)
        {
            var note = _notes[index];
            if (!selected.Contains(note))
            {
                selected.Add(note);
            }
        }

        return selected;
    }

    private void CompleteActiveResourceChange()
    {
        var completedChange = _activeChange;
        if (completedChange == null)
        {
            return;
        }

        if (_notes.Count != completedChange.TargetAmount)
        {
            SetAmountImmediately(completedChange.TargetAmount);
        }

        _activeChange = null;
        if (completedChange.PreviewRevision == _previewRevision)
        {
            ApplyPreviewAmount(completedChange.PreviewAmountAfter);
        }
    }

    private void SetAmountImmediately(int amount)
    {
        ClearAllNotes();
        for (var i = 0; i < amount; i++)
        {
            _notes.Add(CreateNote(visible: true));
        }

        AssignEvenSlots(immediately: true);
    }

    private void ClearAllNotes()
    {
        foreach (var note in _notes)
        {
            if (IsInstanceValid(note.Sprite))
            {
                note.Sprite.QueueFree();
            }
        }

        _notes.Clear();
        _previewNotes.Clear();
    }

    private void ClearEffectLayer()
    {
        if (_effectLayer == null)
        {
            return;
        }

        foreach (var child in _effectLayer.GetChildren())
        {
            child.QueueFree();
        }
    }

    private OrbitNote CreateNote(bool visible)
    {
        var sprite = CreateNoteSprite();
        var motionBlurSprites = CreatePreviewMotionBlurSprites(sprite);
        sprite.Visible = visible;
        (_frontLayer ?? this).AddChild(sprite);
        return new OrbitNote(sprite, motionBlurSprites)
        {
            PreviewPhase = Rng.Chaotic.NextFloat(0f, Mathf.Tau),
            PopElapsed = visible ? -1f : 0f,
            FadeInElapsed = -1f
        };
    }

    private Sprite2D CreateNoteSprite()
    {
        if (_noteTemplate == null)
        {
            throw new InvalidOperationException("Note template is not initialized.");
        }

        var sprite = (Sprite2D)_noteTemplate.Duplicate();
        sprite.Visible = true;
        sprite.Modulate = Colors.White;
        sprite.Frame = Rng.Chaotic.NextInt(0, 8);
        return sprite;
    }

    private static Sprite2D[] CreatePreviewMotionBlurSprites(Sprite2D source)
    {
        var nearTrail = (Sprite2D)source.Duplicate();
        var farTrail = (Sprite2D)source.Duplicate();
        ConfigurePreviewMotionBlurSprite(nearTrail);
        ConfigurePreviewMotionBlurSprite(farTrail);
        source.AddChild(farTrail);
        source.AddChild(nearTrail);
        return [nearTrail, farTrail];
    }

    private static void ConfigurePreviewMotionBlurSprite(Sprite2D sprite)
    {
        sprite.Visible = false;
        sprite.ShowBehindParent = true;
    }

    private Sprite2D CreateParticleSprite(float scale)
    {
        if (_particleTemplate == null)
        {
            throw new InvalidOperationException("Particle template is not initialized.");
        }

        var sprite = (Sprite2D)_particleTemplate.Duplicate();
        sprite.Visible = true;
        sprite.Scale = Vector2.One * scale;
        return sprite;
    }

    private void AssignEvenSlots(bool immediately = false)
    {
        if (_notes.Count == 0)
        {
            return;
        }

        for (var index = 0; index < _notes.Count; index++)
        {
            var target = Mathf.Tau * index / _notes.Count;
            _notes[index].TargetSlotOffset = target;
            if (immediately || !_notes[index].Sprite.Visible)
            {
                _notes[index].CurrentSlotOffset = target;
            }
        }
    }

    private void UpdateNotes(float delta)
    {
        foreach (var note in _notes)
        {
            note.CurrentSlotOffset = Mathf.LerpAngle(
                note.CurrentSlotOffset,
                note.TargetSlotOffset,
                1f - Mathf.Exp(-LayoutFollowSpeed * delta));

            var orbitAngle = _orbitPhase + note.CurrentSlotOffset;
            var orbitPosition = GetOrbitPosition(orbitAngle);
            var depth = Mathf.Sin(orbitAngle);
            var isPreviewed = _previewNotes.Contains(note);
            note.PreviewBlend = Mathf.MoveToward(
                note.PreviewBlend,
                isPreviewed ? 1f : 0f,
                (isPreviewed ? PreviewRiseSpeed : PreviewReturnSpeed) * delta);

            var position = orbitPosition;
            if (note.PreviewBlend > 0f)
            {
                var previewIndex = _previewNotes.IndexOf(note);
                var centeredIndex = previewIndex - (_previewNotes.Count - 1) * 0.5f;
                var headPosition = new Vector2(
                    centeredIndex * 34f,
                    -_previewHeight + Mathf.Abs(centeredIndex) * 4f);
                var shake = new Vector2(
                    Mathf.Sin(Time.GetTicksMsec() * 0.009f + note.PreviewPhase) * 2.2f,
                    Mathf.Cos(Time.GetTicksMsec() * 0.011f + note.PreviewPhase) * 1.5f);
                position = orbitPosition.Lerp(headPosition + shake, SmoothStep(note.PreviewBlend));
            }

            note.Sprite.Position = position;

            var popScale = UpdatePopScale(note, delta);
            var depthScale = depth < 0f
                ? Mathf.Lerp(0.7f, 0.86f, depth + 1f)
                : Mathf.Lerp(0.9f, 1.04f, depth);
            var scale = note.PreviewBlend > 0f
                ? NoteBaseScale
                : NoteBaseScale * depthScale * popScale;
            note.Sprite.Scale = Vector2.One * scale;
            UpdatePreviewMotionBlur(note, position);

            var backBrightness = Mathf.Lerp(0.52f, 0.76f, depth + 1f);
            var brightness = depth < 0f ? backBrightness : 1f;
            brightness = Mathf.Lerp(brightness, 1f, note.PreviewBlend);
            note.Sprite.Modulate = new Color(
                brightness,
                brightness,
                brightness,
                UpdateFadeInAlpha(note, delta));

            var targetLayer = depth < 0f && note.PreviewBlend < 0.5f ? _backLayer : _frontLayer;
            if (targetLayer != null && note.Sprite.GetParent() != targetLayer)
            {
                note.Sprite.Reparent(targetLayer, keepGlobalTransform: true);
            }
        }
    }

    private static void UpdatePreviewMotionBlur(OrbitNote note, Vector2 position)
    {
        var motion = note.HasPreviousPosition
            ? note.PreviousPosition - position
            : Vector2.Zero;
        note.PreviousPosition = position;
        note.HasPreviousPosition = true;

        const float maxTrailLength = 14f;
        if (motion.LengthSquared() > maxTrailLength * maxTrailLength)
        {
            motion = motion.Normalized() * maxTrailLength;
        }

        var blurStrength = SmoothStep(note.PreviewBlend);
        var isVisible = blurStrength > 0.02f && motion.LengthSquared() > 0.08f;
        var scale = Math.Max(note.Sprite.Scale.X, 0.01f);
        var nearTrail = note.MotionBlurSprites[0];
        nearTrail.Visible = isVisible;
        nearTrail.Position = motion * 0.55f / scale;
        nearTrail.SelfModulate = new Color(0.72f, 0.88f, 1f, blurStrength * 0.16f);

        var farTrail = note.MotionBlurSprites[1];
        farTrail.Visible = isVisible;
        farTrail.Position = motion / scale;
        farTrail.SelfModulate = new Color(0.62f, 0.82f, 1f, blurStrength * 0.07f);
    }

    private static float UpdatePopScale(OrbitNote note, float delta)
    {
        if (note.PopElapsed < 0f)
        {
            return 1f;
        }

        note.PopElapsed += delta;
        if (note.PopElapsed < 0.16f)
        {
            return Mathf.Lerp(0f, 1.15f, SmoothStep(note.PopElapsed / 0.16f));
        }

        if (note.PopElapsed < 0.34f)
        {
            return Mathf.Lerp(1.15f, 1f, SmoothStep((note.PopElapsed - 0.16f) / 0.18f));
        }

        note.PopElapsed = -1f;
        return 1f;
    }

    private static float UpdateFadeInAlpha(OrbitNote note, float delta)
    {
        if (note.FadeInElapsed < 0f)
        {
            return 1f;
        }

        note.FadeInElapsed += delta;
        var alpha = SmoothStep(note.FadeInElapsed / NoteFadeInDuration);
        if (note.FadeInElapsed >= NoteFadeInDuration)
        {
            note.FadeInElapsed = -1f;
            return 1f;
        }

        return alpha;
    }

    private void ApplyPreviewAmount(int amount)
    {
        var availableNotes = _notes.Where(note => note.Sprite.Visible).ToList();
        if (amount <= 0 || amount > availableNotes.Count)
        {
            _previewNotes.Clear();
            return;
        }

        var retained = _previewNotes
            .Where(availableNotes.Contains)
            .Take(amount)
            .ToList();
        foreach (var note in availableNotes)
        {
            if (retained.Count == amount)
            {
                break;
            }

            if (!retained.Contains(note))
            {
                retained.Add(note);
            }
        }

        _previewNotes.Clear();
        _previewNotes.AddRange(retained);
    }

    private void UpdatePreviewCleanup(float delta)
    {
        if (_previewCleanupRemaining < 0f)
        {
            return;
        }

        if (_pendingChanges.Count > 0 || _gatherBatch != null || _explosionBatch != null)
        {
            return;
        }

        _previewCleanupRemaining -= delta;
        if (_previewCleanupRemaining <= 0f)
        {
            _previewRevision++;
            ClearPreview();
        }
    }

    private void ClearPreview()
    {
        _previewNotes.Clear();
        _previewCleanupRemaining = -1f;
    }

    private void UpdateCreatureBoundsMetrics()
    {
        if (_creature == null)
        {
            return;
        }

        var bounds = _creature.Visuals.GetNodeOrNull<Control>("%Bounds");
        var boundsSize = bounds?.Size ?? _creature.Hitbox.Size;
        if (boundsSize.X <= 0f || boundsSize.Y <= 0f)
        {
            return;
        }

        _orbitRadius = new Vector2(
            Mathf.Clamp(boundsSize.X * 0.36f, 56f, 200f),
            Mathf.Clamp(boundsSize.Y * 0.13f, 24f, 72f));
        _previewHeight = Mathf.Clamp(boundsSize.Y * 0.6f + 32f, 108f, 280f);
    }

    private void SyncLayersToCreature()
    {
        if (_creature == null || !IsInstanceValid(_creature))
        {
            return;
        }

        var scale = _creature.Visuals.Scale;
        var position = _creature.VfxSpawnPosition;
        if (_backLayer != null && IsInstanceValid(_backLayer))
        {
            _backLayer.Scale = scale;
            _backLayer.GlobalPosition = position;
        }

        if (_frontLayer != null && IsInstanceValid(_frontLayer))
        {
            _frontLayer.Scale = scale;
            _frontLayer.GlobalPosition = position;
        }

        if (_effectLayer != null && IsInstanceValid(_effectLayer))
        {
            _effectLayer.Scale = scale;
            _effectLayer.GlobalPosition = position;
        }
    }

    private void AttachVisualLayersToCreature()
    {
        if (_creature == null || _backLayer == null || _frontLayer == null || _effectLayer == null)
        {
            return;
        }

        var visuals = _creature.Visuals;
        var visualParent = visuals.GetParent();
        if (visualParent == null)
        {
            return;
        }

        ConfigureLayerForVisuals(_backLayer, visuals);
        ConfigureLayerForVisuals(_frontLayer, visuals);
        ConfigureLayerForVisuals(_effectLayer, visuals);

        _backLayer.Reparent(visualParent, keepGlobalTransform: true);
        visualParent.MoveChild(_backLayer, visuals.GetIndex());

        _frontLayer.Reparent(visualParent, keepGlobalTransform: true);
        visualParent.MoveChild(_frontLayer, visuals.GetIndex() + 1);

        _effectLayer.Reparent(visualParent, keepGlobalTransform: true);
        visualParent.MoveChild(_effectLayer, _frontLayer.GetIndex() + 1);
    }

    private static void ConfigureLayerForVisuals(CanvasItem layer, CanvasItem visuals)
    {
        layer.ZAsRelative = visuals.ZAsRelative;
        layer.ZIndex = visuals.ZIndex;
    }

    private static void QueueFreeDetachedLayer(Node2D? layer)
    {
        if (layer != null && IsInstanceValid(layer) && !layer.IsQueuedForDeletion())
        {
            layer.QueueFree();
        }
    }

    private Vector2 GetOrbitPosition(float angle)
    {
        return new Vector2(
            Mathf.Cos(angle) * _orbitRadius.X,
            Mathf.Sin(angle) * _orbitRadius.Y);
    }

    private static float SmoothStep(float value)
    {
        value = Mathf.Clamp(value, 0f, 1f);
        return value * value * (3f - 2f * value);
    }

    private static float EaseOutCubic(float value)
    {
        value = 1f - Mathf.Clamp(value, 0f, 1f);
        return 1f - value * value * value;
    }

    private static Vector2 QuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float progress)
    {
        var inverse = 1f - progress;
        return inverse * inverse * start +
               2f * inverse * progress * control +
               progress * progress * end;
    }

    private sealed class OrbitNote(Sprite2D sprite, Sprite2D[] motionBlurSprites)
    {
        public Sprite2D Sprite { get; } = sprite;
        public Sprite2D[] MotionBlurSprites { get; } = motionBlurSprites;
        public float CurrentSlotOffset { get; set; }
        public float TargetSlotOffset { get; set; }
        public float PreviewBlend { get; set; }
        public float PreviewPhase { get; set; }
        public float PopElapsed { get; set; }
        public float FadeInElapsed { get; set; }
        public Vector2 PreviousPosition { get; set; }
        public bool HasPreviousPosition { get; set; }
    }

    private sealed record ResourceVisualChange(
        int TargetAmount,
        int PreviewAmountAfter,
        int PreviewRevision);

    private sealed class GatherBatch(
        List<OrbitNote> newNotes,
        List<GatherParticle> particles)
    {
        public List<OrbitNote> NewNotes { get; } = newNotes;
        public List<GatherParticle> Particles { get; } = particles;
        public float Elapsed { get; set; }
    }

    private sealed record GatherParticle(
        Sprite2D Sprite,
        OrbitNote TargetNote,
        Vector2 Start,
        Vector2 Control,
        float StartScale,
        float Delay);

    private sealed class ExplosionBatch(List<ExplosionBurst> bursts)
    {
        public List<ExplosionBurst> Bursts { get; } = bursts;
        public float Elapsed { get; set; }
    }

    private sealed record ExplosionBurst(
        Sprite2D Note,
        Sprite2D Flash,
        Vector2 StartScale,
        Color StartModulate,
        List<ExplosionParticle> Particles);

    private sealed record ExplosionParticle(
        Sprite2D Sprite,
        Vector2 Start,
        Vector2 Velocity,
        float StartScale);
}
