using System.Collections.Concurrent;
using RogueLikeNet.Client.Core.Rendering;
using RogueLikeNet.Client.Core.State;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Systems;
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

    public void Drain(ClientGameState gameState, ParticleSystem particles, ChatSystem? chat = null)
    {
        while (_pendingDeltas.TryDequeue(out var delta))
        {
            FirstDeltaProcessed = true;
            gameState.ApplyDelta(delta);
        }

        foreach (var evt in gameState.PendingCombatEvents)
        {
            if (evt.Blocked)
            {
                particles.SpawnBlockText(evt.TargetX, evt.TargetY);
            }
            else
            {
                particles.SpawnDamageNumber(evt.TargetX, evt.TargetY, evt.Damage, evt.TargetDied);
                particles.SpawnHitSparks(evt.AttackerX, evt.AttackerY, evt.TargetX, evt.TargetY, evt.TargetDied);
            }
        }
        gameState.DrainCombatEvents();

        foreach (var dlg in gameState.PendingNpcDialogues)
        {
            chat?.AddChatLine($"[{dlg.NpcName}]: {dlg.Text}");
        }

        foreach (var evt in gameState.PendingPlayerActionEvents)
        {
            var msg = FormatPlayerActionEvent(evt);
            if (msg != null)
                chat?.AddChatLine(msg);
        }

        gameState.DrainNpcDialogues();
        gameState.DrainPlayerActionEvents();
    }

    public void Reset()
    {
        FirstDeltaProcessed = false;
        while (_pendingDeltas.TryDequeue(out _)) { }
    }

    private static string? FormatPlayerActionEvent(PlayerActionEventMsg evt)
    {
        var eventType = (PlayerActionEventType)evt.EventType;
        var itemName = GetItemName(evt.ItemTypeId);

        if (evt.Failed)
        {
            return eventType switch
            {
                PlayerActionEventType.PickUp => evt.FailReason switch
                {
                    1 => "Inventory is full",     // ActionFailReason.InventoryFull
                    2 => "Nothing to pick up",    // ActionFailReason.NoItemsOnGround
                    _ => "Failed to pick up items",
                },
                PlayerActionEventType.Drop => "Failed to drop item",
                PlayerActionEventType.UsePotion => evt.ItemTypeId != 0 ? $"Failed to use {itemName}" : "Failed to use item",
                PlayerActionEventType.EatFood => evt.ItemTypeId != 0 ? $"Failed to eat {itemName}" : "Failed to eat item",
                PlayerActionEventType.Equip => evt.ItemTypeId != 0 ? $"Failed to equip {itemName}" : "Failed to equip item",
                PlayerActionEventType.Unequip => "Failed to unequip item",
                PlayerActionEventType.PlaceItem => evt.ItemTypeId != 0 ? $"Failed to place {itemName}" : "Failed to place item",
                PlayerActionEventType.PickUpPlaced => "Failed to pick up item",
                PlayerActionEventType.Till => "Failed to till soil",
                PlayerActionEventType.Plant => evt.ItemTypeId != 0 ? $"Failed to plant {itemName}" : "Failed to plant",
                PlayerActionEventType.Water => "Failed to water crop",
                PlayerActionEventType.Harvest => "Failed to harvest",
                PlayerActionEventType.FeedAnimal => "Failed to feed animal",
                PlayerActionEventType.Craft => evt.ItemTypeId != 0 ? $"Failed to craft {itemName}" : "Failed to craft",
                _ => null,
            };
        }

        return eventType switch
        {
            PlayerActionEventType.PickUp => evt.StackCount > 1
                ? $"Picked up {itemName} x{evt.StackCount}"
                : $"Picked up {itemName}",
            PlayerActionEventType.Drop => $"Dropped {itemName}{(evt.StackCount > 1 ? $" x{evt.StackCount}" : "")}",
            PlayerActionEventType.UsePotion => $"Used {itemName}",
            PlayerActionEventType.EatFood => $"Ate {itemName}",
            PlayerActionEventType.Equip => $"Equipped {itemName}",
            PlayerActionEventType.Unequip => $"Unequipped {itemName}",
            PlayerActionEventType.PlaceItem => $"Placed {itemName}",
            PlayerActionEventType.PickUpPlaced => $"Picked up {itemName}{(evt.StackCount > 1 ? $" x{evt.StackCount}" : "")}",
            PlayerActionEventType.Till => "Tilled soil",
            PlayerActionEventType.Plant => $"Planted {itemName}",
            PlayerActionEventType.Water => "Watered crop",
            PlayerActionEventType.Harvest => evt.StackCount > 1
                ? $"Harvested {itemName} x{evt.StackCount}"
                : $"Harvested {itemName}",
            PlayerActionEventType.FeedAnimal => $"Fed animal with {itemName}",
            PlayerActionEventType.Craft => evt.StackCount > 1
                ? $"Crafted {itemName} x{evt.StackCount}"
                : $"Crafted {itemName}",
            _ => null,
        };
    }

    private static string GetItemName(int itemTypeId)
    {
        if (itemTypeId == 0) return "unknown";
        var def = GameData.Instance.Items.Get(itemTypeId);
        return def?.Name ?? "unknown";
    }
}
