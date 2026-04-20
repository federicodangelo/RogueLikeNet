using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Handles buy/sell transactions between players and shop NPCs.
/// Requires the player to be adjacent to (or on top of) a town NPC whose role has a shop.
/// </summary>
public class TradingSystem
{
    private const string GoldItemId = "gold_coin";
    private const int MaxProximity = 2; // tiles

    public void Update(WorldMap worldMap)
    {
        foreach (ref var player in worldMap.Players)
        {
            if (player.IsDead) continue;

            switch (player.Input.ActionType)
            {
                case ActionTypes.BuyItem:
                    ProcessBuy(ref player, worldMap);
                    break;
                case ActionTypes.SellItem:
                    ProcessSell(ref player, worldMap);
                    break;
            }
        }
    }

    private static void ProcessBuy(ref PlayerEntity player, WorldMap worldMap)
    {
        player.Input.ActionType = ActionTypes.None;

        int shopEntryIndex = player.Input.ItemSlot;
        var npcRole = (TownNpcRole)player.Input.TargetSlot;

        var shopDef = GameData.Instance.Shops.GetByRole(npcRole);
        if (shopDef == null) return;
        if (shopEntryIndex < 0 || shopEntryIndex >= shopDef.Items.Length) return;

        // Verify NPC proximity
        if (!IsNearShopNpc(ref player, worldMap, npcRole)) return;

        var entry = shopDef.Items[shopEntryIndex];
        var itemDef = GameData.Instance.Items.Get(entry.ItemId);
        if (itemDef == null) return;

        int price = entry.Price;
        int goldId = GameData.Instance.Items.GetNumericId(GoldItemId);
        if (goldId == 0) return;

        // Check player has enough gold
        int goldCount = CountItem(ref player, goldId);
        if (goldCount < price)
        {
            player.ActionEvents.Add(new PlayerActionEvent
            {
                EventType = PlayerActionEventType.Buy,
                ItemTypeId = itemDef.NumericId,
                Failed = true,
            });
            return;
        }

        // Check inventory space
        if (player.Inventory.IsFull)
        {
            player.ActionEvents.Add(new PlayerActionEvent
            {
                EventType = PlayerActionEventType.Buy,
                ItemTypeId = itemDef.NumericId,
                Failed = true,
                FailReason = ActionFailReason.InventoryFull,
            });
            return;
        }

        // Deduct gold
        RemoveItems(ref player, goldId, price);

        // Add purchased item
        var purchasedItem = new ItemData { ItemTypeId = itemDef.NumericId, StackCount = 1 };
        InventorySystem.AddItemToInventory(ref player, purchasedItem);

        player.ActionEvents.Add(new PlayerActionEvent
        {
            EventType = PlayerActionEventType.Buy,
            ItemTypeId = itemDef.NumericId,
            StackCount = 1,
        });
    }

    private static void ProcessSell(ref PlayerEntity player, WorldMap worldMap)
    {
        player.Input.ActionType = ActionTypes.None;

        int invSlot = player.Input.ItemSlot;
        var npcRole = (TownNpcRole)player.Input.TargetSlot;

        if (invSlot < 0 || invSlot >= player.Inventory.Items.Count) return;

        var shopDef = GameData.Instance.Shops.GetByRole(npcRole);
        if (shopDef == null) return;

        // Verify NPC proximity
        if (!IsNearShopNpc(ref player, worldMap, npcRole)) return;

        var item = player.Inventory.Items[invSlot];
        var itemDef = GameData.Instance.Items.Get(item.ItemTypeId);
        if (itemDef == null) return;

        // Don't allow selling gold coins
        int goldId = GameData.Instance.Items.GetNumericId(GoldItemId);
        if (item.ItemTypeId == goldId) return;

        // Calculate sell price: find if the shop buys this item, else use base formula
        int sellPrice = CalculateSellPrice(shopDef, itemDef);
        if (sellPrice <= 0) sellPrice = 1; // minimum 1 gold

        // Remove the item (1 from stack or entire slot)
        if (item.StackCount > 1)
        {
            item.StackCount--;
            player.Inventory.Items[invSlot] = item;
        }
        else
        {
            player.Inventory.Items.RemoveAt(invSlot);
            // Fix quick slots that pointed to removed/shifted indices
            player.QuickSlots.OnItemRemoved(invSlot);
        }

        // Add gold
        var goldData = new ItemData { ItemTypeId = goldId, StackCount = sellPrice };
        InventorySystem.AddItemToInventory(ref player, goldData);

        player.ActionEvents.Add(new PlayerActionEvent
        {
            EventType = PlayerActionEventType.Sell,
            ItemTypeId = itemDef.NumericId,
            StackCount = 1,
        });
    }

    private static int CalculateSellPrice(ShopDefinition shopDef, ItemDefinition itemDef)
    {
        // Check if this shop sells this item — use its buy price * sellPricePercent
        foreach (var entry in shopDef.Items)
        {
            if (entry.ItemId == itemDef.Id)
                return Math.Max(1, entry.Price * shopDef.SellPricePercent / 100);
        }

        // Generic sell price: 1 gold for misc items
        return 1;
    }

    private static bool IsNearShopNpc(ref PlayerEntity player, WorldMap worldMap, TownNpcRole role)
    {
        // Check the player's chunk and nearby loaded chunks for a matching NPC
        foreach (var chunk in worldMap.LoadedChunks)
        {
            foreach (ref var npc in chunk.TownNpcs)
            {
                if (npc.IsDead) continue;
                if (npc.NpcData.Role != role) continue;
                int dx = Math.Abs(npc.Position.X - player.Position.X);
                int dy = Math.Abs(npc.Position.Y - player.Position.Y);
                if (dx <= MaxProximity && dy <= MaxProximity && npc.Position.Z == player.Position.Z)
                    return true;
            }
        }

        return false;
    }

    private static int CountItem(ref PlayerEntity player, int itemTypeId)
    {
        int count = 0;
        for (int i = 0; i < player.Inventory.Items.Count; i++)
        {
            if (player.Inventory.Items[i].ItemTypeId == itemTypeId)
                count += player.Inventory.Items[i].StackCount;
        }
        return count;
    }

    private static void RemoveItems(ref PlayerEntity player, int itemTypeId, int amount)
    {
        int remaining = amount;
        for (int i = player.Inventory.Items.Count - 1; i >= 0 && remaining > 0; i--)
        {
            if (player.Inventory.Items[i].ItemTypeId != itemTypeId) continue;

            var item = player.Inventory.Items[i];
            if (item.StackCount <= remaining)
            {
                remaining -= item.StackCount;
                player.Inventory.Items.RemoveAt(i);
                player.QuickSlots.OnItemRemoved(i);
            }
            else
            {
                item.StackCount -= remaining;
                player.Inventory.Items[i] = item;
                remaining = 0;
            }
        }
    }
}
