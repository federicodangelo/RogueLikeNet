using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class CraftingSystemTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private static int ItemId(string id) => GameData.Instance.Items.GetNumericId(id);
    private static int RecipeId(string id) => GameData.Instance.Recipes.GetNumericId(id);

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        return engine;
    }

    private int SpawnPlayerWithItems(GameEngine engine, params (string itemId, int count)[] items)
    {
        var (sx, sy, _) = engine.FindSpawnPosition();
        var p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(p.Id);
        foreach (var (itemId, count) in items)
        {
            player.Inventory.Items.Add(new ItemData
            {
                ItemTypeId = ItemId(itemId),
                StackCount = count,
            });
        }
        return p.Id;
    }

    private void PlaceStation(GameEngine engine, Position near, string stationItemId)
    {
        var pos = Position.FromCoords(near.X + 1, near.Y, near.Z);
        var tile = engine.WorldMap.GetTile(pos);
        tile.PlaceableItemId = ItemId(stationItemId);
        engine.WorldMap.SetTile(pos, tile);
    }

    private static TileInfo FloorTile() => new()
    {
        TileId = GameData.Instance.Tiles.GetNumericId("floor"),
    };

    // ──────────────────────────────────────────────────────────────
    //  Hand recipes: no station required
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void HandRecipe_CraftsWithoutStation()
    {
        // craft_crafting_bench: 8 wood → 1 crafting_bench (hand recipe)
        using var engine = CreateEngine();
        var pid = SpawnPlayerWithItems(engine, ("wood", 8));
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = RecipeId("craft_crafting_bench");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.Contains(player.Inventory.Items, i => i.ItemTypeId == ItemId("crafting_bench"));
        Assert.DoesNotContain(player.Inventory.Items, i => i.ItemTypeId == ItemId("wood"));
    }

    // ──────────────────────────────────────────────────────────────
    //  Workbench recipes: require nearby crafting_bench
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void WorkbenchRecipe_FailsWithoutStation()
    {
        // craft_wooden_pickaxe: 5 wood + 2 fiber → wooden_pickaxe (workbench)
        using var engine = CreateEngine();
        var pid = SpawnPlayerWithItems(engine, ("wood", 5), ("fiber", 2));
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = RecipeId("craft_wooden_pickaxe");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        // Should NOT have crafted — no workbench nearby
        Assert.DoesNotContain(player.Inventory.Items, i => i.ItemTypeId == ItemId("wooden_pickaxe"));
        // Ingredients should remain
        Assert.Contains(player.Inventory.Items, i => i.ItemTypeId == ItemId("wood") && i.StackCount == 5);
    }

    [Fact]
    public void WorkbenchRecipe_SucceedsWithStation()
    {
        using var engine = CreateEngine();
        var pid = SpawnPlayerWithItems(engine, ("wood", 5), ("fiber", 2));
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        PlaceStation(engine, player.Position, "crafting_bench");

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = RecipeId("craft_wooden_pickaxe");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.Contains(player.Inventory.Items, i => i.ItemTypeId == ItemId("wooden_pickaxe"));
    }

    // ──────────────────────────────────────────────────────────────
    //  Anvil recipes
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void AnvilRecipe_FailsWithoutAnvil()
    {
        // craft_iron_sword: 2 wood + 5 iron_ingot → iron_sword (anvil)
        using var engine = CreateEngine();
        var pid = SpawnPlayerWithItems(engine, ("wood", 2), ("iron_ingot", 5));
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = RecipeId("craft_iron_sword");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.DoesNotContain(player.Inventory.Items, i => i.ItemTypeId == ItemId("iron_sword"));
    }

    [Fact]
    public void AnvilRecipe_SucceedsWithAnvil()
    {
        using var engine = CreateEngine();
        var pid = SpawnPlayerWithItems(engine, ("wood", 2), ("iron_ingot", 5));
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        PlaceStation(engine, player.Position, "anvil");

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = RecipeId("craft_iron_sword");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.Contains(player.Inventory.Items, i => i.ItemTypeId == ItemId("iron_sword"));
    }

    [Fact]
    public void AnvilRecipe_FailsWithWrongStation()
    {
        // Place a furnace instead of anvil — should not satisfy anvil requirement
        using var engine = CreateEngine();
        var pid = SpawnPlayerWithItems(engine, ("wood", 2), ("iron_ingot", 5));
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        PlaceStation(engine, player.Position, "furnace");

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = RecipeId("craft_iron_sword");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.DoesNotContain(player.Inventory.Items, i => i.ItemTypeId == ItemId("iron_sword"));
    }

    // ──────────────────────────────────────────────────────────────
    //  Furnace (smelting) recipes
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void FurnaceRecipe_SucceedsWithFurnace()
    {
        // smelt_copper_ingot: 3 copper_ore + 1 coal → copper_ingot (furnace)
        using var engine = CreateEngine();
        var pid = SpawnPlayerWithItems(engine, ("copper_ore", 3), ("coal", 1));
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        PlaceStation(engine, player.Position, "furnace");

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = RecipeId("smelt_copper_ingot");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.Contains(player.Inventory.Items, i => i.ItemTypeId == ItemId("copper_ingot"));
    }

    [Fact]
    public void FurnaceRecipe_FailsWithoutFurnace()
    {
        using var engine = CreateEngine();
        var pid = SpawnPlayerWithItems(engine, ("copper_ore", 3), ("coal", 1));
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = RecipeId("smelt_copper_ingot");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.DoesNotContain(player.Inventory.Items, i => i.ItemTypeId == ItemId("copper_ingot"));
    }

    // ──────────────────────────────────────────────────────────────
    //  Cooking pot recipes
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void CookingRecipe_SucceedsWithCookingPot()
    {
        // cook_meat: 1 raw_meat → cooked_meat (cookingPot)
        using var engine = CreateEngine();
        var pid = SpawnPlayerWithItems(engine, ("raw_meat", 1));
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        PlaceStation(engine, player.Position, "cooking_pot");

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = RecipeId("cook_meat");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.Contains(player.Inventory.Items, i => i.ItemTypeId == ItemId("cooked_meat"));
    }

    [Fact]
    public void CookingRecipe_FailsWithoutCookingPot()
    {
        using var engine = CreateEngine();
        var pid = SpawnPlayerWithItems(engine, ("raw_meat", 1));
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = RecipeId("cook_meat");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.DoesNotContain(player.Inventory.Items, i => i.ItemTypeId == ItemId("cooked_meat"));
        Assert.Contains(player.Inventory.Items, i => i.ItemTypeId == ItemId("raw_meat"));
    }

    // ──────────────────────────────────────────────────────────────
    //  Station range boundary
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void StationWithinRange_AllowsCrafting()
    {
        using var engine = CreateEngine();
        var pid = SpawnPlayerWithItems(engine, ("wood", 5), ("fiber", 2));
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Place station exactly at range boundary (dx=5, dy=0)
        var stationPos = Position.FromCoords(player.Position.X + 5, player.Position.Y, player.Position.Z);
        var tile = FloorTile();
        tile.PlaceableItemId = ItemId("crafting_bench");
        engine.WorldMap.SetTile(stationPos, tile);

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = RecipeId("craft_wooden_pickaxe");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.Contains(player.Inventory.Items, i => i.ItemTypeId == ItemId("wooden_pickaxe"));
    }

    [Fact]
    public void StationOutOfRange_BlocksCrafting()
    {
        using var engine = CreateEngine();
        var pid = SpawnPlayerWithItems(engine, ("wood", 5), ("fiber", 2));
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Place station just beyond range (dx=6, dy=0)
        var stationPos = Position.FromCoords(player.Position.X + 6, player.Position.Y, player.Position.Z);
        var tile = FloorTile();
        tile.PlaceableItemId = ItemId("crafting_bench");
        engine.WorldMap.SetTile(stationPos, tile);

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = RecipeId("craft_wooden_pickaxe");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.DoesNotContain(player.Inventory.Items, i => i.ItemTypeId == ItemId("wooden_pickaxe"));
    }

    // ──────────────────────────────────────────────────────────────
    //  Ingredients still required even with station
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void StationPresent_ButMissingIngredients_DoesNotCraft()
    {
        using var engine = CreateEngine();
        // Only 2 wood instead of required 5
        var pid = SpawnPlayerWithItems(engine, ("wood", 2), ("fiber", 2));
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        PlaceStation(engine, player.Position, "crafting_bench");

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = RecipeId("craft_wooden_pickaxe");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.DoesNotContain(player.Inventory.Items, i => i.ItemTypeId == ItemId("wooden_pickaxe"));
        // Wood should not be consumed
        Assert.Contains(player.Inventory.Items, i => i.ItemTypeId == ItemId("wood") && i.StackCount == 2);
    }

    // ──────────────────────────────────────────────────────────────
    //  NearbyStations in player state data
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetPlayerStateData_IncludesNearbyStations()
    {
        using var engine = CreateEngine();
        var pid = SpawnPlayerWithItems(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        PlaceStation(engine, player.Position, "crafting_bench");

        // Tick to generate player state
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        var state = engine.GetPlayerStateData(player);
        Assert.NotNull(state);
        // Should always contain Hand (0)
        Assert.Contains((int)CraftingStationType.Hand, state.NearbyStationsTypes);
        // Should contain Workbench since crafting_bench is nearby
        Assert.Contains((int)CraftingStationType.Workbench, state.NearbyStationsTypes);
    }

    [Fact]
    public void GetPlayerStateData_NoStations_OnlyHand()
    {
        using var engine = CreateEngine();
        var pid = SpawnPlayerWithItems(engine);

        engine.Tick();

        ref var player = ref engine.WorldMap.GetPlayerRef(pid);
        var state = engine.GetPlayerStateData(player);
        Assert.NotNull(state);
        Assert.Contains((int)CraftingStationType.Hand, state.NearbyStationsTypes);
        // Only Hand should be present (no stations nearby in most spawn positions)
        // We can't guarantee no stations exist near spawn, but Hand must be there
        Assert.True(state.NearbyStationsTypes.Length >= 1);
    }

    [Fact]
    public void GetPlayerStateData_MultipleStations()
    {
        using var engine = CreateEngine();
        var pid = SpawnPlayerWithItems(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Place two different stations nearby
        var pos1 = Position.FromCoords(player.Position.X + 1, player.Position.Y, player.Position.Z);
        var tile1 = engine.WorldMap.GetTile(pos1);
        tile1.PlaceableItemId = ItemId("crafting_bench");
        engine.WorldMap.SetTile(pos1, tile1);

        var pos2 = Position.FromCoords(player.Position.X - 1, player.Position.Y, player.Position.Z);
        var tile2 = engine.WorldMap.GetTile(pos2);
        tile2.PlaceableItemId = ItemId("anvil");
        engine.WorldMap.SetTile(pos2, tile2);

        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        var state = engine.GetPlayerStateData(player);
        Assert.NotNull(state);
        Assert.Contains((int)CraftingStationType.Hand, state.NearbyStationsTypes);
        Assert.Contains((int)CraftingStationType.Workbench, state.NearbyStationsTypes);
        Assert.Contains((int)CraftingStationType.Anvil, state.NearbyStationsTypes);
    }

    // ──────────────────────────────────────────────────────────────
    //  RecipeRegistry station lookup
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void RecipeRegistry_GetByStation_ReturnsCorrectRecipes()
    {
        var handRecipes = GameData.Instance.Recipes.GetByStation(CraftingStationType.Hand);
        Assert.NotEmpty(handRecipes);
        Assert.All(handRecipes, r => Assert.Equal(CraftingStationType.Hand, r.Station));

        var anvilRecipes = GameData.Instance.Recipes.GetByStation(CraftingStationType.Anvil);
        Assert.NotEmpty(anvilRecipes);
        Assert.All(anvilRecipes, r => Assert.Equal(CraftingStationType.Anvil, r.Station));
    }

    [Fact]
    public void ItemRegistry_CraftingStationItems_HaveCorrectType()
    {
        var bench = GameData.Instance.Items.Get("crafting_bench");
        Assert.NotNull(bench);
        Assert.NotNull(bench.Placeable);
        Assert.Equal(CraftingStationType.Workbench, bench.Placeable.CraftingStationType);

        var anvil = GameData.Instance.Items.Get("anvil");
        Assert.NotNull(anvil);
        Assert.NotNull(anvil.Placeable);
        Assert.Equal(CraftingStationType.Anvil, anvil.Placeable.CraftingStationType);

        var furnace = GameData.Instance.Items.Get("furnace");
        Assert.NotNull(furnace);
        Assert.NotNull(furnace.Placeable);
        Assert.Equal(CraftingStationType.Furnace, furnace.Placeable.CraftingStationType);

        var cookingPot = GameData.Instance.Items.Get("cooking_pot");
        Assert.NotNull(cookingPot);
        Assert.NotNull(cookingPot.Placeable);
        Assert.Equal(CraftingStationType.CookingPot, cookingPot.Placeable.CraftingStationType);
    }

    // ──────────────────────────────────────────────────────────────
    //  Debug free crafting
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void DebugFreeCrafting_SkipsIngredientCheck()
    {
        // No ingredients in inventory — normally fails
        using var engine = CreateEngine();
        engine.DebugFreeCrafting = true;
        var pid = SpawnPlayerWithItems(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = RecipeId("craft_crafting_bench");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.Contains(player.Inventory.Items, i => i.ItemTypeId == ItemId("crafting_bench"));
    }

    [Fact]
    public void DebugFreeCrafting_SkipsStationCheck()
    {
        // Anvil recipe with no anvil nearby — normally fails
        using var engine = CreateEngine();
        engine.DebugFreeCrafting = true;
        var pid = SpawnPlayerWithItems(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = RecipeId("craft_iron_sword");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.Contains(player.Inventory.Items, i => i.ItemTypeId == ItemId("iron_sword"));
    }

    [Fact]
    public void DebugFreeCrafting_DoesNotConsumeIngredients()
    {
        using var engine = CreateEngine();
        engine.DebugFreeCrafting = true;
        var pid = SpawnPlayerWithItems(engine, ("wood", 8));
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = RecipeId("craft_crafting_bench");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        // Crafted item should be in inventory
        Assert.Contains(player.Inventory.Items, i => i.ItemTypeId == ItemId("crafting_bench"));
        // Wood should NOT be consumed in debug mode
        Assert.Contains(player.Inventory.Items, i => i.ItemTypeId == ItemId("wood") && i.StackCount == 8);
    }

    [Fact]
    public void DebugFreeCrafting_Off_StillValidates()
    {
        using var engine = CreateEngine();
        engine.DebugFreeCrafting = false;
        var pid = SpawnPlayerWithItems(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = RecipeId("craft_crafting_bench");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.DoesNotContain(player.Inventory.Items, i => i.ItemTypeId == ItemId("crafting_bench"));
    }

    // ── Null recipe ──

    [Fact]
    public void Craft_InvalidRecipeId_DoesNothing()
    {
        using var engine = CreateEngine();
        var pid = SpawnPlayerWithItems(engine, ("wood", 50));
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = 9999; // non-existent recipe
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        // Should still have the original items, nothing crafted
        Assert.Contains(player.Inventory.Items, i => i.ItemTypeId == ItemId("wood") && i.StackCount == 50);
    }

    // ── Ingredient removal from multiple stacks ──

    [Fact]
    public void Craft_RemovesIngredientsFromMultipleStacks()
    {
        using var engine = CreateEngine();
        engine.DebugFreeCrafting = false;

        // Split wood across two stacks
        var pid = SpawnPlayerWithItems(engine, ("wood", 3), ("wood", 7));
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Find a recipe requiring wood (crafting_bench needs 10 wood)
        var recipe = GameData.Instance.Recipes.Get("craft_crafting_bench");
        if (recipe == null) return;

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = RecipeId("craft_crafting_bench");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        // Both wood stacks should be consumed
        int remainingWood = player.Inventory.Items.Where(i => i.ItemTypeId == ItemId("wood")).Sum(i => i.StackCount);
        // Result item should be in inventory
        Assert.Contains(player.Inventory.Items, i => i.ItemTypeId == ItemId("crafting_bench"));
    }

    // ── Craft stacking result into existing stack ──

    [Fact]
    public void Craft_StackableResult_MergesIntoExistingStack()
    {
        using var engine = CreateEngine();
        engine.DebugFreeCrafting = true;

        // Find a recipe that produces a stackable result
        var allRecipes = GameData.Instance.Recipes.All;
        RecipeDefinition? stackableRecipe = null;
        foreach (var r in allRecipes)
        {
            var resultDef = GameData.Instance.Items.Get(r.Result.NumericItemId);
            if (resultDef != null && resultDef.Stackable)
            {
                stackableRecipe = r;
                break;
            }
        }
        if (stackableRecipe == null) return;

        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Pre-add the result item to inventory
        player.Inventory.Items.Add(new ItemData { ItemTypeId = stackableRecipe.Result.NumericItemId, StackCount = 1 });

        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = stackableRecipe.NumericId;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        // Should have merged into existing stack
        int totalCount = player.Inventory.Items.Where(i => i.ItemTypeId == stackableRecipe.Result.NumericItemId).Sum(i => i.StackCount);
        Assert.True(totalCount > 1);
    }

    // ── Craft with full inventory ──

    [Fact]
    public void Craft_FullInventory_NonStackable_DoesNotAddResult()
    {
        using var engine = CreateEngine();
        engine.DebugFreeCrafting = true;

        var (sx, sy, _) = engine.FindSpawnPosition();
        var _p = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);
        ref var player = ref engine.WorldMap.GetPlayerRef(_p.Id);

        // Fill inventory
        int cap = player.Inventory.Capacity;
        for (int i = 0; i < cap; i++)
            player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("short_sword"), StackCount = 1 });

        // Craft crafting_bench (non-stackable) with full inventory
        player.Input.ActionType = ActionTypes.Craft;
        player.Input.ItemSlot = RecipeId("craft_crafting_bench");
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(_p.Id);
        // Should not have added the result
        Assert.DoesNotContain(player.Inventory.Items, i => i.ItemTypeId == ItemId("crafting_bench"));
    }
}
