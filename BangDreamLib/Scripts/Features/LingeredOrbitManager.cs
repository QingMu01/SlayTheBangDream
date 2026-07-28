using BangDreamLib.Scripts.Interfaces;
using BangDreamLib.Scripts.Interfaces.CardAugment;
using BangDreamLib.Scripts.Interfaces.CharacterAugment;
using BangDreamLib.Scripts.Nodes.VFX;
using BangDreamLib.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

namespace BangDreamLib.Scripts.Features;

public class LingeredOrbitManager : SingletonModel, IInCombatManager
{
    private Player? _player;
    private CardModel? _previewCard;
    private bool _isSubscribed;

    public override bool ShouldReceiveCombatHooks => false;

    public Player Player
    {
        get => _player ?? throw new InvalidOperationException("Owner is not initialized.");
        set
        {
            AssertMutable();
            BangDreamTools.Init(ref _player, value, nameof(Player));
        }
    }

    public NLingeredOrbitVfx? OrbitVfx { get; private set; }

    public void SubmitCombatState()
    {
        UnsubscribeResourceChanges();
        _previewCard = null;
        if (Player.Character is not ILingeredResourceCharacter)
        {
            OrbitVfx = null;
            return;
        }

        var creatureNode = Player.Creature.GetCreatureNode();
        if (creatureNode == null ||
            !ModNodeAttachmentRegistry.For(BangDreamConst.ModId)
                .TryGetAttached<NCreature, NLingeredOrbitVfx>(
                    creatureNode,
                    NLingeredOrbitVfx.AttachmentId,
                    out var orbitVfx))
        {
            OrbitVfx = null;
            return;
        }

        OrbitVfx = orbitVfx;
        OrbitVfx.Initialize(Math.Max(0,
            SecondaryResourceCmd.Get(Player, BangDreamConst.LingeredResource)));
        SecondaryResourceStateStore.Get(Player).Changed += OnSecondaryResourceChanged;
        _isSubscribed = true;
    }

    public void UnsubscribeCombatState()
    {
        UnsubscribeResourceChanges();
        _previewCard = null;
        if (OrbitVfx != null && GodotObject.IsInstanceValid(OrbitVfx))
        {
            OrbitVfx.Shutdown();
        }

        OrbitVfx = null;
    }

    private void OnSecondaryResourceChanged(SecondaryResourceChangedEvent changedEvent)
    {
        if (!changedEvent.Definition.Id.Equals(BangDreamConst.LingeredResource))
        {
            return;
        }

        GetActiveOrbitVfx()?.SubmitResourceAmount(
            Math.Max(0, changedEvent.NewAmount),
            GetPreviewPaymentAmount(_previewCard));
    }

    private void UnsubscribeResourceChanges()
    {
        if (!_isSubscribed)
        {
            return;
        }

        if (_player != null && SecondaryResourceStateStore.TryGet(_player, out var resourceState))
        {
            resourceState.Changed -= OnSecondaryResourceChanged;
        }

        _isSubscribed = false;
    }

    public void BeginSubsidePreview(CardModel card)
    {
        if (card.Owner != _player || card is not ISubsideCard)
        {
            return;
        }

        _previewCard = card;
        GetActiveOrbitVfx()?.ShowPreview(GetPreviewPaymentAmount(card));
    }

    public void CancelSubsidePreview(CardModel card)
    {
        if (_previewCard != card)
        {
            return;
        }

        _previewCard = null;
        GetActiveOrbitVfx()?.CancelPreview();
    }

    public void ScheduleSubsidePreviewCleanup(CardModel card)
    {
        if (_previewCard != card)
        {
            return;
        }

        _previewCard = null;
        GetActiveOrbitVfx()?.SchedulePreviewCleanup();
    }

    private NLingeredOrbitVfx? GetActiveOrbitVfx()
    {
        return OrbitVfx != null && GodotObject.IsInstanceValid(OrbitVfx)
            ? OrbitVfx
            : null;
    }

    private static int GetPreviewPaymentAmount(CardModel? card)
    {
        if (card == null)
        {
            return 0;
        }

        var lines = SecondaryResourcePaymentResolver.Plan(card).Lines
            .Where(line => line.ResourceId.Equals(BangDreamConst.LingeredResource))
            .ToList();
        if (lines.Count == 0 || lines.Any(line => !line.IsAffordable))
        {
            return 0;
        }

        return lines.Sum(line => line.AmountToSpend);
    }
}
