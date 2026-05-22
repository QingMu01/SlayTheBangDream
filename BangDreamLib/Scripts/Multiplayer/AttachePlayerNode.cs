using BangDreamLib.Scripts.Nodes;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Utils;

namespace BangDreamLib.Scripts.Multiplayer;

public class AttachePlayerNode
{
    public static readonly AttachedState<NCreature, AttachePlayerNode>
        State = new(creature => new AttachePlayerNode(creature));

    public NPerformanceManager PerformanceManager { get; private set; }

    private AttachePlayerNode(NCreature creature)
    {
        if (creature.Entity.Player == null)
            throw new InvalidOperationException("attache node can only be applied to player's creature");
        PerformanceManager = NPerformanceManager.Create(creature, LocalContext.IsMe(creature.Entity.Player));
    }
}