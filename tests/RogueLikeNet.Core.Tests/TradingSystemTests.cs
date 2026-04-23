using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class TradingSystemTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        return engine;
    }

    private static int ItemId(string id) => GameData.Instance.Items.GetNumericId(id);

    [Fact]
    public void ShopsExist_InGameData()
    {
        var merchantShop = GameData.Instance.Shops.GetByRole(TownNpcRole.Merchant);
        Assert.NotNull(merchantShop);
        Assert.True(merchantShop.Items.Length > 0);

        var blacksmithShop = GameData.Instance.Shops.GetByRole(TownNpcRole.Blacksmith);
        Assert.NotNull(blacksmithShop);
        Assert.True(blacksmithShop.Items.Length > 0);
    }

    [Fact]
    public void BuyItem_WithSufficientGold_AddsItemToInventory()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Spawn a merchant NPC nearby
        engine.SpawnTownNpc(
            Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            "Merchant", sx, sy, 5, TownNpcRole.Merchant);

        // Give player enough gold
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("gold_coin"), StackCount = 500 });

        // Get the merchant shop
        var shop = GameData.Instance.Shops.GetByRole(TownNpcRole.Merchant)!;
        var firstItem = shop.Items[0];
        int expectedItemId = GameData.Instance.Items.GetNumericId(firstItem.ItemId);

        // Buy first item
        player.Input.ActionType = ActionTypes.BuyItem;
        player.Input.ItemSlot = 0; // first shop entry
        player.Input.TargetSlot = (int)TownNpcRole.Merchant;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Should have the bought item
        Assert.Contains(player.Inventory.Items, i => i.ItemTypeId == expectedItemId);

        // Gold should be reduced
        var goldItem = player.Inventory.Items.FirstOrDefault(i => i.ItemTypeId == ItemId("gold_coin"));
        Assert.True(goldItem.StackCount < 500);

        // Should have a Buy action event
        Assert.Contains(player.ActionEvents, e => e.EventType == PlayerActionEventType.Buy && !e.Failed);
    }

    [Fact]
    public void BuyItem_WithInsufficientGold_FailsWithEvent()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        engine.SpawnTownNpc(
            Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            "Merchant", sx, sy, 5, TownNpcRole.Merchant);

        // Give only 1 gold (likely not enough for any shop item)
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("gold_coin"), StackCount = 1 });

        var shop = GameData.Instance.Shops.GetByRole(TownNpcRole.Merchant)!;
        // Find an item that costs more than 1 gold
        int expensiveIdx = -1;
        for (int i = 0; i < shop.Items.Length; i++)
        {
            if (shop.Items[i].Price > 1) { expensiveIdx = i; break; }
        }
        Assert.True(expensiveIdx >= 0, "Need at least one item costing > 1 gold");

        player.Input.ActionType = ActionTypes.BuyItem;
        player.Input.ItemSlot = expensiveIdx;
        player.Input.TargetSlot = (int)TownNpcRole.Merchant;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Should have a failed Buy event
        Assert.Contains(player.ActionEvents, e => e.EventType == PlayerActionEventType.Buy && e.Failed);

        // Gold should not be deducted
        Assert.Equal(1, player.Inventory.Items.First(i => i.ItemTypeId == ItemId("gold_coin")).StackCount);
    }

    [Fact]
    public void BuyItem_NoNpcNearby_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // No NPC spawned — just try to buy
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("gold_coin"), StackCount = 500 });

        player.Input.ActionType = ActionTypes.BuyItem;
        player.Input.ItemSlot = 0;
        player.Input.TargetSlot = (int)TownNpcRole.Merchant;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // No events, gold untouched
        Assert.DoesNotContain(player.ActionEvents, e => e.EventType == PlayerActionEventType.Buy);
        Assert.Equal(500, player.Inventory.Items[0].StackCount);
    }

    [Fact]
    public void SellItem_AddsGoldToInventory()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        engine.SpawnTownNpc(
            Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            "Merchant", sx, sy, 5, TownNpcRole.Merchant);

        // Give player an item to sell
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("wooden_arrow"), StackCount = 5 });
        int initialCount = player.Inventory.Items.Count;

        player.Input.ActionType = ActionTypes.SellItem;
        player.Input.ItemSlot = 0; // inventory slot 0
        player.Input.TargetSlot = (int)TownNpcRole.Merchant;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Should have gold from selling
        Assert.Contains(player.Inventory.Items, i => i.ItemTypeId == ItemId("gold_coin"));

        // Should have a Sell action event
        Assert.Contains(player.ActionEvents, e => e.EventType == PlayerActionEventType.Sell);
    }

    [Fact]
    public void SellItem_CannotSellGoldCoins()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        engine.SpawnTownNpc(
            Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            "Merchant", sx, sy, 5, TownNpcRole.Merchant);

        // Only gold in inventory
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("gold_coin"), StackCount = 50 });

        player.Input.ActionType = ActionTypes.SellItem;
        player.Input.ItemSlot = 0;
        player.Input.TargetSlot = (int)TownNpcRole.Merchant;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Gold should remain unchanged
        Assert.Equal(50, player.Inventory.Items[0].StackCount);
        Assert.DoesNotContain(player.ActionEvents, e => e.EventType == PlayerActionEventType.Sell);
    }

    [Fact]
    public void SellItem_StackableItem_DecrementsStack()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        engine.SpawnTownNpc(
            Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            "Merchant", sx, sy, 5, TownNpcRole.Merchant);

        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("wooden_arrow"), StackCount = 5 });

        player.Input.ActionType = ActionTypes.SellItem;
        player.Input.ItemSlot = 0;
        player.Input.TargetSlot = (int)TownNpcRole.Merchant;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Stack should be decremented by 1
        var arrows = player.Inventory.Items.FirstOrDefault(i => i.ItemTypeId == ItemId("wooden_arrow"));
        Assert.Equal(4, arrows.StackCount);
    }

    [Fact]
    public void BuyItem_DifferentShopRoles_WorkCorrectly()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Spawn a blacksmith
        engine.SpawnTownNpc(
            Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            "Blacksmith", sx, sy, 5, TownNpcRole.Blacksmith);

        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("gold_coin"), StackCount = 5000 });

        var shop = GameData.Instance.Shops.GetByRole(TownNpcRole.Blacksmith)!;
        var firstItem = shop.Items[0];

        player.Input.ActionType = ActionTypes.BuyItem;
        player.Input.ItemSlot = 0;
        player.Input.TargetSlot = (int)TownNpcRole.Blacksmith;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);

        int expectedItemId = GameData.Instance.Items.GetNumericId(firstItem.ItemId);
        Assert.Contains(player.Inventory.Items, i => i.ItemTypeId == expectedItemId);
    }

    [Fact]
    public void SellItem_PriceCalculation_ShopItemsGiveHigherPrice()
    {
        // Items that the shop sells should give higher sell prices (based on sellPricePercent)
        // while unknown items should give minimum price (1 gold)
        var merchantShop = GameData.Instance.Shops.GetByRole(TownNpcRole.Merchant)!;
        Assert.True(merchantShop.SellPricePercent > 0);

        // Verify a shop item would yield more than 1 gold
        var expensiveEntry = merchantShop.Items.FirstOrDefault(e => e.Price > 10);
        if (expensiveEntry != null)
        {
            int expectedSellPrice = Math.Max(1, expensiveEntry.Price * merchantShop.SellPricePercent / 100);
            Assert.True(expectedSellPrice > 1);
        }
    }

    [Fact]
    public void BuyItem_FullInventory_FailsWithEvent()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        engine.SpawnTownNpc(
            Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            "Merchant", sx, sy, 5, TownNpcRole.Merchant);

        // Fill inventory to capacity (30 slots)
        for (int i = 0; i < 30; i++)
        {
            player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("wooden_arrow"), StackCount = 1 });
        }
        // Replace one slot with gold for the purchase
        player.Inventory.Items[0] = new ItemData { ItemTypeId = ItemId("gold_coin"), StackCount = 5000 };
        Assert.True(player.Inventory.IsFull);

        player.Input.ActionType = ActionTypes.BuyItem;
        player.Input.ItemSlot = 0; // first shop entry
        player.Input.TargetSlot = (int)TownNpcRole.Merchant;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Should fail because inventory is full
        Assert.Contains(player.ActionEvents,
            e => e.EventType == PlayerActionEventType.Buy && e.Failed && e.FailReason == ActionFailReason.InventoryFull);
    }

    [Fact]
    public void BuyItem_InvalidShopEntryIndex_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        engine.SpawnTownNpc(
            Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            "Merchant", sx, sy, 5, TownNpcRole.Merchant);

        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("gold_coin"), StackCount = 5000 });

        // Negative index
        player.Input.ActionType = ActionTypes.BuyItem;
        player.Input.ItemSlot = -1;
        player.Input.TargetSlot = (int)TownNpcRole.Merchant;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.DoesNotContain(player.ActionEvents, e => e.EventType == PlayerActionEventType.Buy);
        Assert.Equal(5000, player.Inventory.Items[0].StackCount);
    }

    [Fact]
    public void BuyItem_ShopEntryIndexTooLarge_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        engine.SpawnTownNpc(
            Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            "Merchant", sx, sy, 5, TownNpcRole.Merchant);

        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("gold_coin"), StackCount = 5000 });

        player.Input.ActionType = ActionTypes.BuyItem;
        player.Input.ItemSlot = 9999; // way beyond shop items
        player.Input.TargetSlot = (int)TownNpcRole.Merchant;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.DoesNotContain(player.ActionEvents, e => e.EventType == PlayerActionEventType.Buy);
    }

    [Fact]
    public void SellItem_InvalidSlot_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        engine.SpawnTownNpc(
            Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            "Merchant", sx, sy, 5, TownNpcRole.Merchant);

        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("wooden_arrow"), StackCount = 5 });

        // Try to sell from slot beyond inventory
        player.Input.ActionType = ActionTypes.SellItem;
        player.Input.ItemSlot = 999;
        player.Input.TargetSlot = (int)TownNpcRole.Merchant;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.DoesNotContain(player.ActionEvents, e => e.EventType == PlayerActionEventType.Sell);
        Assert.Equal(5, player.Inventory.Items[0].StackCount);
    }

    [Fact]
    public void SellItem_NegativeSlot_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        engine.SpawnTownNpc(
            Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            "Merchant", sx, sy, 5, TownNpcRole.Merchant);

        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("wooden_arrow"), StackCount = 5 });

        player.Input.ActionType = ActionTypes.SellItem;
        player.Input.ItemSlot = -1;
        player.Input.TargetSlot = (int)TownNpcRole.Merchant;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.DoesNotContain(player.ActionEvents, e => e.EventType == PlayerActionEventType.Sell);
    }

    [Fact]
    public void SellItem_NoNpcNearby_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // No NPC spawned
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("wooden_arrow"), StackCount = 5 });

        player.Input.ActionType = ActionTypes.SellItem;
        player.Input.ItemSlot = 0;
        player.Input.TargetSlot = (int)TownNpcRole.Merchant;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.DoesNotContain(player.ActionEvents, e => e.EventType == PlayerActionEventType.Sell);
        Assert.Equal(5, player.Inventory.Items[0].StackCount);
    }

    [Fact]
    public void SellItem_SingleStackItem_RemovesFromInventory()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        engine.SpawnTownNpc(
            Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            "Merchant", sx, sy, 5, TownNpcRole.Merchant);

        // Single item (stack=1)
        int arrowId = ItemId("wooden_arrow");
        player.Inventory.Items.Add(new ItemData { ItemTypeId = arrowId, StackCount = 1 });

        player.Input.ActionType = ActionTypes.SellItem;
        player.Input.ItemSlot = 0;
        player.Input.TargetSlot = (int)TownNpcRole.Merchant;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Arrow should be removed entirely, gold added
        Assert.DoesNotContain(player.Inventory.Items, i => i.ItemTypeId == arrowId);
        Assert.Contains(player.Inventory.Items, i => i.ItemTypeId == ItemId("gold_coin"));
    }

    [Fact]
    public void SellItem_UnknownItemToShop_GetsMinimumPrice()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        engine.SpawnTownNpc(
            Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            "Merchant", sx, sy, 5, TownNpcRole.Merchant);

        // Sell an item the merchant doesn't stock (e.g. a raw material)
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("raw_meat"), StackCount = 1 });

        player.Input.ActionType = ActionTypes.SellItem;
        player.Input.ItemSlot = 0;
        player.Input.TargetSlot = (int)TownNpcRole.Merchant;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Should get minimum 1 gold
        Assert.Contains(player.ActionEvents, e => e.EventType == PlayerActionEventType.Sell);
        var goldItem = player.Inventory.Items.FirstOrDefault(i => i.ItemTypeId == ItemId("gold_coin"));
        Assert.True(goldItem.StackCount >= 1);
    }

    [Fact]
    public void BuyItem_InvalidShopRole_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Spawn a Villager (no shop)
        engine.SpawnTownNpc(
            Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            "Villager", sx, sy, 5, TownNpcRole.Villager);

        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("gold_coin"), StackCount = 5000 });

        player.Input.ActionType = ActionTypes.BuyItem;
        player.Input.ItemSlot = 0;
        player.Input.TargetSlot = (int)TownNpcRole.Villager;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.DoesNotContain(player.ActionEvents, e => e.EventType == PlayerActionEventType.Buy);
        Assert.Equal(5000, player.Inventory.Items[0].StackCount);
    }

    [Fact]
    public void SellItem_InvalidShopRole_DoesNothing()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        // Spawn a Villager (no shop)
        engine.SpawnTownNpc(
            Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            "Villager", sx, sy, 5, TownNpcRole.Villager);

        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("wooden_arrow"), StackCount = 5 });

        player.Input.ActionType = ActionTypes.SellItem;
        player.Input.ItemSlot = 0;
        player.Input.TargetSlot = (int)TownNpcRole.Villager;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.DoesNotContain(player.ActionEvents, e => e.EventType == PlayerActionEventType.Sell);
        Assert.Equal(5, player.Inventory.Items[0].StackCount);
    }

    [Fact]
    public void BuyItem_GoldSpreadAcrossMultipleStacks_DeductsCorrectly()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);

        engine.SpawnTownNpc(
            Position.FromCoords(sx + 1, sy, Position.DefaultZ),
            "Merchant", sx, sy, 5, TownNpcRole.Merchant);

        var shop = GameData.Instance.Shops.GetByRole(TownNpcRole.Merchant)!;
        var firstItem = shop.Items[0];
        int price = firstItem.Price;

        // Spread gold across two stacks that together cover the price
        int goldId = ItemId("gold_coin");
        player.Inventory.Items.Add(new ItemData { ItemTypeId = goldId, StackCount = price / 2 + 1 });
        player.Inventory.Items.Add(new ItemData { ItemTypeId = goldId, StackCount = price });
        int totalGoldBefore = player.Inventory.Items.Where(i => i.ItemTypeId == goldId).Sum(i => i.StackCount);

        player.Input.ActionType = ActionTypes.BuyItem;
        player.Input.ItemSlot = 0;
        player.Input.TargetSlot = (int)TownNpcRole.Merchant;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(p.Id);
        Assert.Contains(player.ActionEvents, e => e.EventType == PlayerActionEventType.Buy && !e.Failed);

        int totalGoldAfter = player.Inventory.Items.Where(i => i.ItemTypeId == goldId).Sum(i => i.StackCount);
        Assert.Equal(totalGoldBefore - price, totalGoldAfter);
    }
}
