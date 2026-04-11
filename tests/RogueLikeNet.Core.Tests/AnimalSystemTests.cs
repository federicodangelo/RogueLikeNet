using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class AnimalSystemTests
{
    private static readonly BspDungeonGenerator _gen = new(42);

    private static int ItemId(string id) => GameData.Instance.Items.GetNumericId(id);

    private GameEngine CreateEngine()
    {
        var engine = new GameEngine(42, _gen);
        engine.EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        return engine;
    }

    private (int PlayerId, Position AnimalPos) SpawnPlayerAndAnimal(GameEngine engine, string animalType = "chicken")
    {
        var (sx, sy, _) = engine.FindSpawnPosition();
        var player = engine.SpawnPlayer(1, Position.FromCoords(sx, sy, Position.DefaultZ), ClassDefinitions.Warrior);

        // Ensure adjacent tile is walkable
        var animalPos = Position.FromCoords(sx + 1, sy, Position.DefaultZ);
        engine.WorldMap.SetTile(animalPos, new TileInfo
        {
            TileId = GameData.Instance.Tiles.GetNumericId("floor"),
        });

        // Spawn animal
        var def = GameData.Instance.Animals.Get(animalType)!;
        ref var testAnimal = ref engine.SpawnAnimal(animalPos, def);
        testAnimal.MoveDelay.Current = 9999; // prevent moving so it stays in place for the test

        // Give player correct feed
        ref var p = ref engine.WorldMap.GetPlayerRef(player.Id);
        p.Inventory.Items.Add(new ItemData
        {
            ItemTypeId = ItemId("animal_feed"),
            StackCount = 10,
        });

        return (player.Id, animalPos);
    }

    // ── Feeding ──

    [Fact]
    public void Feed_SetsFedStatusWithCorrectItem()
    {
        using var engine = CreateEngine();
        var (pid, animalPos) = SpawnPlayerAndAnimal(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        player.Input.ActionType = ActionTypes.FeedAnimal;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        var chunk = engine.WorldMap.GetChunkForWorldPos(animalPos)!;
        var foundFed = false;
        foreach (var animal in chunk.Animals)
        {
            if (!animal.IsDead && animal.Position == animalPos)
            {
                Assert.True(animal.AnimalData.IsFed);
                Assert.True(animal.AnimalData.FedTicksRemaining > 0);
                foundFed = true;
                break;
            }
        }
        Assert.True(foundFed, "Expected a fed animal at position");

        // Feed item should be consumed
        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.Equal(9, player.Inventory.Items[0].StackCount);
    }

    [Fact]
    public void Feed_FailsWithWrongItem()
    {
        using var engine = CreateEngine();
        var (pid, animalPos) = SpawnPlayerAndAnimal(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Replace feed with a non-feed item
        player.Inventory.Items[0] = new ItemData
        {
            ItemTypeId = ItemId("wood"),
            StackCount = 5,
        };

        player.Input.ActionType = ActionTypes.FeedAnimal;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        var chunk = engine.WorldMap.GetChunkForWorldPos(animalPos)!;
        foreach (var animal in chunk.Animals)
        {
            if (!animal.IsDead && animal.Position == animalPos)
            {
                Assert.False(animal.AnimalData.IsFed);
                return;
            }
        }
    }

    [Fact]
    public void Feed_RequiresAdjacentAnimal()
    {
        using var engine = CreateEngine();
        var (pid, animalPos) = SpawnPlayerAndAnimal(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Target far away (non-adjacent)
        player.Input.ActionType = ActionTypes.FeedAnimal;
        player.Input.TargetX = 2;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Feed should not be consumed
        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.Equal(10, player.Inventory.Items[0].StackCount);
    }

    // ── Production ──

    [Fact]
    public void FedAnimal_ProducesItemAfterInterval()
    {
        using var engine = CreateEngine();
        var (pid, animalPos) = SpawnPlayerAndAnimal(engine);

        // Feed the animal
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.FeedAnimal;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Fast-forward production timer to just before production
        var chunk = engine.WorldMap.GetChunkForWorldPos(animalPos)!;
        foreach (ref var animal in chunk.Animals)
        {
            if (!animal.IsDead && animal.Position == animalPos)
            {
                // Set production ticks just before threshold
                var chickenDef = GameData.Instance.Animals.Get(animal.AnimalData.AnimalTypeId)!;
                animal.AnimalData.ProduceTicksCurrent = chickenDef.ProduceIntervalTicks - 1;
                break;
            }
        }

        // Count ground items before
        int groundItemsBefore = chunk.GroundItems.Length;

        // One more tick should trigger production
        engine.Tick();

        // Should have a new ground item near the animal
        int groundItemsAfter = chunk.GroundItems.Length;
        Assert.True(groundItemsAfter > groundItemsBefore, "Expected a produced item to drop on ground");

        // Verify it's the correct produce type (egg for chicken)
        var eggId = ItemId("egg");
        var foundProduce = false;
        foreach (var item in chunk.GroundItems)
        {
            if (item.Item.ItemTypeId == eggId)
            {
                foundProduce = true;
                break;
            }
        }
        Assert.True(foundProduce, "Expected an egg ground item from chicken");
    }

    [Fact]
    public void UnfedAnimal_DoesNotProduce()
    {
        using var engine = CreateEngine();
        var (pid, animalPos) = SpawnPlayerAndAnimal(engine);

        // Don't feed, just advance many ticks
        var chunk = engine.WorldMap.GetChunkForWorldPos(animalPos)!;
        foreach (ref var animal in chunk.Animals)
        {
            if (!animal.IsDead && animal.Position == animalPos)
            {
                // Set production ticks high, but not fed
                var chickenDef = GameData.Instance.Animals.Get(animal.AnimalData.AnimalTypeId)!;
                animal.AnimalData.ProduceTicksCurrent = chickenDef.ProduceIntervalTicks + 10;
                break;
            }
        }

        int groundItemsBefore = chunk.GroundItems.Length;
        engine.Tick();
        int groundItemsAfter = chunk.GroundItems.Length;

        Assert.Equal(groundItemsBefore, groundItemsAfter);
    }

    // ── Fed expiry ──

    [Fact]
    public void FedStatus_ExpiresAfterDuration()
    {
        using var engine = CreateEngine();
        var (pid, animalPos) = SpawnPlayerAndAnimal(engine);

        // Feed the animal
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.FeedAnimal;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        player.Input.ItemSlot = 0;
        engine.Tick();

        // Fast-forward fed ticks to just before expiry
        var chunk = engine.WorldMap.GetChunkForWorldPos(animalPos)!;
        foreach (ref var animal in chunk.Animals)
        {
            if (!animal.IsDead && animal.Position == animalPos)
            {
                animal.AnimalData.FedTicksRemaining = 1;
                break;
            }
        }

        // One tick → fed expires
        engine.Tick();

        foreach (var animal in chunk.Animals)
        {
            if (!animal.IsDead && animal.Position == animalPos)
            {
                Assert.False(animal.AnimalData.IsFed);
                Assert.Equal(0, animal.AnimalData.ProduceTicksCurrent);
                return;
            }
        }
    }

    // ── Breeding ──

    [Fact]
    public void TwoFedSameTypeAnimals_BreedWithinRange()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        // Set up a clear area
        for (int dx = -2; dx <= 4; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                var p = Position.FromCoords(sx + dx, sy + dy, Position.DefaultZ);
                engine.WorldMap.SetTile(p, new TileInfo
                {
                    TileId = GameData.Instance.Tiles.GetNumericId("floor"),
                });
            }
        }

        var def = GameData.Instance.Animals.Get("chicken")!;
        var pos1 = Position.FromCoords(sx + 1, sy, Position.DefaultZ);
        var pos2 = Position.FromCoords(sx + 2, sy, Position.DefaultZ);

        ref var animal1 = ref engine.SpawnAnimal(pos1, def);
        ref var animal2 = ref engine.SpawnAnimal(pos2, def);

        // Both fed, ready to breed
        animal1.AnimalData.IsFed = true;
        animal1.AnimalData.FedTicksRemaining = 1000;
        animal1.AnimalData.BreedCooldownCurrent = 0;

        animal2.AnimalData.IsFed = true;
        animal2.AnimalData.FedTicksRemaining = 1000;
        animal2.AnimalData.BreedCooldownCurrent = 0;

        var chunk = engine.WorldMap.GetChunkForWorldPos(pos1)!;
        int animalCountBefore = CountLiveAnimals(chunk);

        engine.Tick();

        int animalCountAfter = CountLiveAnimals(chunk);
        Assert.True(animalCountAfter > animalCountBefore, "Expected a new animal from breeding");
    }

    [Fact]
    public void Breeding_AppliesCooldownToBothParents()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        // Set up clear area
        for (int dx = -2; dx <= 4; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                var p = Position.FromCoords(sx + dx, sy + dy, Position.DefaultZ);
                engine.WorldMap.SetTile(p, new TileInfo
                {
                    TileId = GameData.Instance.Tiles.GetNumericId("floor"),
                });
            }
        }

        var def = GameData.Instance.Animals.Get("chicken")!;
        var pos1 = Position.FromCoords(sx + 1, sy, Position.DefaultZ);
        var pos2 = Position.FromCoords(sx + 2, sy, Position.DefaultZ);

        ref var a1 = ref engine.SpawnAnimal(pos1, def);
        ref var a2 = ref engine.SpawnAnimal(pos2, def);

        a1.AnimalData.IsFed = true;
        a1.AnimalData.FedTicksRemaining = 1000;
        a1.AnimalData.BreedCooldownCurrent = 0;

        a2.AnimalData.IsFed = true;
        a2.AnimalData.FedTicksRemaining = 1000;
        a2.AnimalData.BreedCooldownCurrent = 0;

        engine.Tick();

        // Both parents should have cooldown set
        var chunk = engine.WorldMap.GetChunkForWorldPos(pos1)!;
        foreach (var animal in chunk.Animals)
        {
            if (!animal.IsDead && (animal.Position == pos1 || animal.Position == pos2))
            {
                // Cooldown decremented by 1 in the same tick, so it should be BreedCooldownTicks - 1
                // (or BreedCooldownTicks if decrement happens before breeding)
                Assert.True(animal.AnimalData.BreedCooldownCurrent > 0,
                    $"Expected breed cooldown > 0 but was {animal.AnimalData.BreedCooldownCurrent}");
            }
        }
    }

    [Fact]
    public void Breeding_PreventsWithCooldownActive()
    {
        using var engine = CreateEngine();
        var (sx, sy, _) = engine.FindSpawnPosition();

        for (int dx = -2; dx <= 4; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                var p = Position.FromCoords(sx + dx, sy + dy, Position.DefaultZ);
                engine.WorldMap.SetTile(p, new TileInfo
                {
                    TileId = GameData.Instance.Tiles.GetNumericId("floor"),
                });
            }
        }

        var def = GameData.Instance.Animals.Get("chicken")!;
        var pos1 = Position.FromCoords(sx + 1, sy, Position.DefaultZ);
        var pos2 = Position.FromCoords(sx + 2, sy, Position.DefaultZ);

        ref var a1 = ref engine.SpawnAnimal(pos1, def);
        ref var a2 = ref engine.SpawnAnimal(pos2, def);

        // Fed but with active breed cooldown
        a1.AnimalData.IsFed = true;
        a1.AnimalData.FedTicksRemaining = 1000;
        a1.AnimalData.BreedCooldownCurrent = 999; // active cooldown

        a2.AnimalData.IsFed = true;
        a2.AnimalData.FedTicksRemaining = 1000;
        a2.AnimalData.BreedCooldownCurrent = 999;

        var chunk = engine.WorldMap.GetChunkForWorldPos(pos1)!;
        int animalCountBefore = CountLiveAnimals(chunk);

        engine.Tick();

        int animalCountAfter = CountLiveAnimals(chunk);
        Assert.Equal(animalCountBefore, animalCountAfter);
    }

    // ── AnimalData unit tests ──

    [Fact]
    public void AnimalData_CanProduce_OnlyWhenFedAndTimerReached()
    {
        var data = new AnimalData
        {
            ProduceTicksCurrent = 99,
            IsFed = true,
        };
        var def = new AnimalDefinition
        {
            ProduceIntervalTicks = 100,
        };
        Assert.False(data.CanProduce(def));

        data.ProduceTicksCurrent = 100;
        Assert.True(data.CanProduce(def));

        data.IsFed = false;
        Assert.False(data.CanProduce(def));
    }

    [Fact]
    public void AnimalData_CanBreed_OnlyWhenFedAndNoCooldown()
    {
        var data = new AnimalData
        {
            IsFed = true,
            BreedCooldownCurrent = 0,
        };
        Assert.True(data.CanBreed);

        data.BreedCooldownCurrent = 10;
        Assert.False(data.CanBreed);

        data.BreedCooldownCurrent = 0;
        data.IsFed = false;
        Assert.False(data.CanBreed);
    }

    private static int CountLiveAnimals(Chunk chunk)
    {
        int count = 0;
        foreach (var animal in chunk.Animals)
            if (!animal.IsDead) count++;
        return count;
    }

    // ── Interact (context-sensitive) tests ──

    [Fact]
    public void Interact_OnAnimalWithFeed_FeedsAnimal()
    {
        using var engine = CreateEngine();
        var (pid, animalPos) = SpawnPlayerAndAnimal(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        var chunk = engine.WorldMap.GetChunkForWorldPos(animalPos)!;
        foreach (var animal in chunk.Animals)
        {
            if (!animal.IsDead && animal.Position == animalPos)
            {
                Assert.True(animal.AnimalData.IsFed, "Animal should be fed via Interact");
                Assert.True(animal.AnimalData.FedTicksRemaining > 0);
                return;
            }
        }
        Assert.Fail("Expected a fed animal at position");
    }

    [Fact]
    public void Interact_OnAnimalWithFeed_ConsumesFeedItem()
    {
        using var engine = CreateEngine();
        var (pid, animalPos) = SpawnPlayerAndAnimal(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.Equal(9, player.Inventory.Items[0].StackCount);
    }

    [Fact]
    public void Interact_OnAnimalWithoutFeed_DoesNotFeed()
    {
        using var engine = CreateEngine();
        var (pid, animalPos) = SpawnPlayerAndAnimal(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Remove all feed from inventory
        player.Inventory.Items.Clear();

        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        var chunk = engine.WorldMap.GetChunkForWorldPos(animalPos)!;
        foreach (var animal in chunk.Animals)
        {
            if (!animal.IsDead && animal.Position == animalPos)
            {
                Assert.False(animal.AnimalData.IsFed, "Animal should not be fed without feed item");
                return;
            }
        }
    }

    [Fact]
    public void Interact_OnAnimalWithWrongItem_DoesNotFeed()
    {
        using var engine = CreateEngine();
        var (pid, animalPos) = SpawnPlayerAndAnimal(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Replace feed with a non-feed item
        player.Inventory.Items[0] = new ItemData
        {
            ItemTypeId = ItemId("wood"),
            StackCount = 5,
        };

        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        var chunk = engine.WorldMap.GetChunkForWorldPos(animalPos)!;
        foreach (var animal in chunk.Animals)
        {
            if (!animal.IsDead && animal.Position == animalPos)
            {
                Assert.False(animal.AnimalData.IsFed, "Animal should not be fed with wrong item");
                return;
            }
        }
    }

    [Fact]
    public void Interact_NonAdjacentAnimal_DoesNotFeed()
    {
        using var engine = CreateEngine();
        var (pid, animalPos) = SpawnPlayerAndAnimal(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Target non-adjacent
        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 2;
        player.Input.TargetY = 0;
        engine.Tick();

        // Feed should not be consumed
        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.Equal(10, player.Inventory.Items[0].StackCount);

        // Animal should not be fed
        var chunk = engine.WorldMap.GetChunkForWorldPos(animalPos)!;
        foreach (var animal in chunk.Animals)
        {
            if (!animal.IsDead && animal.Position == animalPos)
            {
                Assert.False(animal.AnimalData.IsFed);
                return;
            }
        }
    }

    [Fact]
    public void Interact_OnDeadAnimal_DoesNotFeed()
    {
        using var engine = CreateEngine();
        var (pid, animalPos) = SpawnPlayerAndAnimal(engine);

        // Kill the animal
        var chunk = engine.WorldMap.GetChunkForWorldPos(animalPos)!;
        foreach (ref var animal in chunk.Animals)
        {
            if (!animal.IsDead && animal.Position == animalPos)
            {
                animal.Health.Current = 0;
                break;
            }
        }

        ref var player = ref engine.WorldMap.GetPlayerRef(pid);
        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        // Feed should not be consumed
        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.Equal(10, player.Inventory.Items[0].StackCount);
    }

    [Fact]
    public void Interact_FindsFeedInInventory_AutomaticallySelectsSlot()
    {
        using var engine = CreateEngine();
        var (pid, animalPos) = SpawnPlayerAndAnimal(engine);
        ref var player = ref engine.WorldMap.GetPlayerRef(pid);

        // Add a non-feed item first, then feed (to test slot auto-selection)
        player.Inventory.Items.Clear();
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("wood"), StackCount = 5 });
        player.Inventory.Items.Add(new ItemData { ItemTypeId = ItemId("animal_feed"), StackCount = 3 });

        player.Input.ActionType = ActionTypes.Interact;
        player.Input.TargetX = 1;
        player.Input.TargetY = 0;
        engine.Tick();

        var chunk = engine.WorldMap.GetChunkForWorldPos(animalPos)!;
        foreach (var animal in chunk.Animals)
        {
            if (!animal.IsDead && animal.Position == animalPos)
            {
                Assert.True(animal.AnimalData.IsFed, "Interact should find feed in inventory and auto-select slot");
                return;
            }
        }
        Assert.Fail("Expected animal at position");

        // Feed consumed from slot 1, wood untouched
        player = ref engine.WorldMap.GetPlayerRef(pid);
        Assert.Equal(5, player.Inventory.Items[0].StackCount); // wood unchanged
        Assert.Equal(2, player.Inventory.Items[1].StackCount); // feed decremented
    }
}
