using System.Collections.Concurrent;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Protocol.Messages;

namespace RogueLikeNet.Client.Core.Systems;

/// <summary>
/// Buffers network world deltas from the network thread and drains them on the main thread.
/// </summary>
public sealed class NetworkMessageDrainer
{
    private readonly ConcurrentQueue<WorldDeltaMsg> _pendingDeltas = new();

    public bool FirstDeltaProcessed { get; private set; }

    public void EnqueueDelta(WorldDeltaMsg delta)
    {
        _pendingDeltas.Enqueue(delta);
    }

    public void Drain(ClientGameState gameState, ParticleSystem particles)
    {
        while (_pendingDeltas.TryDequeue(out var delta))
        {
            FirstDeltaProcessed = true;
            gameState.ApplyDelta(delta);
        }

        foreach (var evt in gameState.PendingCombatEvents)
        {
            particles.SpawnDamageNumber(evt.TargetX, evt.TargetY, evt.Damage, evt.TargetDied);
            particles.SpawnHitSparks(evt.AttackerX, evt.AttackerY, evt.TargetX, evt.TargetY, evt.TargetDied);
        }
        gameState.DrainCombatEvents();
    }

    public void Reset()
    {
        FirstDeltaProcessed = false;
        while (_pendingDeltas.TryDequeue(out _)) { }
    }
}
