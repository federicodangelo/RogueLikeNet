using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Processes animal feeding, timed resource production, and breeding.
/// Animals must be fed to produce resources. Fed animals produce items on a timer.
/// Two fed animals of the same type within range can breed to spawn a new animal.
/// </summary>
public class AnimalSystem
{
    /// <summary>Range within which two animals can breed.</summary>
    public const int BreedRange = 3;

    public void Update(WorldMap map)
    {
        // Process player feeding actions
        foreach (ref var player in map.Players)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType == ActionTypes.FeedAnimal)
                ProcessFeed(ref player, map);
        }

        // Update all animals: production timers, fed status decay, breeding
        foreach (var chunk in map.LoadedChunks)
        {
            foreach (ref var animal in chunk.Animals)
            {
                if (animal.IsDead) continue;

                // Expire fed status
                if (animal.AnimalData.IsFed)
                {
                    animal.AnimalData.FedTicksRemaining--;
                    if (animal.AnimalData.FedTicksRemaining <= 0)
                    {
                        animal.AnimalData.IsFed = false;
                        animal.AnimalData.ProduceTicksCurrent = 0;
                    }
                }

                // Advance production timer when fed
                if (animal.AnimalData.IsFed)
                {
                    animal.AnimalData.ProduceTicksCurrent++;
                }

                // Decrease breed cooldown
                if (animal.AnimalData.BreedCooldownCurrent > 0)
                    animal.AnimalData.BreedCooldownCurrent--;
            }
        }

        // Process production (drops item on ground next to animal)
        ProcessProduction(map);

        // Process breeding
        ProcessBreeding(map);
    }

    private static void ProcessFeed(ref PlayerEntity player, WorldMap map)
    {
        int slot = player.Input.ItemSlot;
        int targetX = player.Position.X + player.Input.TargetX;
        int targetY = player.Position.Y + player.Input.TargetY;
        var target = Position.FromCoords(targetX, targetY, player.Position.Z);
        player.Input.ActionType = ActionTypes.None;

        // Validate inventory slot
        if (slot < 0 || slot >= player.Inventory.Items.Count) return;

        // Must be adjacent
        int dx = target.X - player.Position.X;
        int dy = target.Y - player.Position.Y;
        if (Math.Abs(dx) + Math.Abs(dy) != 1) return;

        // Find animal at target
        var chunk = map.GetChunkForWorldPos(target);
        if (chunk == null) return;

        foreach (ref var animal in chunk.Animals)
        {
            if (animal.IsDead || animal.Position != target) continue;

            var animalDef = GameData.Instance.Animals.Get(animal.AnimalData.AnimalTypeId);
            if (animalDef == null) continue;

            // Check player has the correct feed item
            var feedItemId = GameData.Instance.Items.GetNumericId(animalDef.FeedItemId);
            if (feedItemId == 0) continue;

            var itemData = player.Inventory.Items[slot];
            if (itemData.ItemTypeId != feedItemId) continue;

            // Consume feed item
            var item = player.Inventory.Items[slot];
            item.StackCount--;
            if (item.StackCount <= 0)
            {
                player.Inventory.Items.RemoveAt(slot);
                player.QuickSlots.OnItemRemoved(slot);
            }
            else
            {
                player.Inventory.Items[slot] = item;
            }

            // Feed the animal
            animal.AnimalData.IsFed = true;
            animal.AnimalData.FedTicksRemaining = animalDef.FedDurationTicks;
            return;
        }
    }

    private static void ProcessProduction(WorldMap map)
    {
        var productions = new List<(Position Pos, int ItemTypeId)>();

        foreach (var chunk in map.LoadedChunks)
        {
            foreach (ref var animal in chunk.Animals)
            {
                if (animal.IsDead) continue;
                if (!animal.AnimalData.CanProduce) continue;

                productions.Add((animal.Position, animal.AnimalData.ProduceItemTypeId));
                animal.AnimalData.ProduceTicksCurrent = 0;
            }
        }

        // Spawn produced items as ground items
        foreach (var (pos, itemTypeId) in productions)
        {
            // Find adjacent walkable tile for the drop
            var dropPos = FindAdjacentDropPosition(map, pos);
            var chunk = map.GetChunkForWorldPos(dropPos);
            if (chunk == null) continue;

            var def = GameData.Instance.Items.Get(itemTypeId);
            var groundItem = new GroundItemEntity(map.AllocateEntityId())
            {
                Position = dropPos,
                Appearance = new TileAppearance(def?.GlyphId ?? 0, def?.FgColor ?? 0),
                Item = new ItemData { ItemTypeId = itemTypeId, StackCount = 1 },
            };
            chunk.AddEntity(groundItem);
        }
    }

    private static void ProcessBreeding(WorldMap map)
    {
        var newAnimals = new List<(Position Pos, int AnimalTypeId)>();

        foreach (var chunk in map.LoadedChunks)
        {
            var animals = chunk.Animals;
            for (int i = 0; i < animals.Length; i++)
            {
                ref var a = ref animals[i];
                if (a.IsDead || !a.AnimalData.CanBreed) continue;

                // Find a mate of the same type nearby
                for (int j = i + 1; j < animals.Length; j++)
                {
                    ref var b = ref animals[j];
                    if (b.IsDead || !b.AnimalData.CanBreed) continue;
                    if (a.AnimalData.AnimalTypeId != b.AnimalData.AnimalTypeId) continue;

                    int dist = Math.Max(Math.Abs(a.Position.X - b.Position.X), Math.Abs(a.Position.Y - b.Position.Y));
                    if (dist > BreedRange) continue;

                    // Find a walkable position for the offspring
                    var midX = (a.Position.X + b.Position.X) / 2;
                    var midY = (a.Position.Y + b.Position.Y) / 2;
                    var spawnPos = Position.FromCoords(midX, midY, a.Position.Z);
                    if (!map.IsWalkable(spawnPos) || map.IsPositionOccupiedByEntity(spawnPos))
                    {
                        spawnPos = FindAdjacentDropPosition(map, a.Position);
                        if (map.IsPositionOccupiedByEntity(spawnPos)) continue;
                    }

                    newAnimals.Add((spawnPos, a.AnimalData.AnimalTypeId));

                    // Apply breed cooldown to both parents
                    a.AnimalData.BreedCooldownCurrent = a.AnimalData.BreedCooldownTicks;
                    b.AnimalData.BreedCooldownCurrent = b.AnimalData.BreedCooldownTicks;
                    break; // Each animal can only breed once per tick
                }
            }
        }

        // Spawn offspring
        foreach (var (pos, typeId) in newAnimals)
        {
            var def = GameData.Instance.Animals.Get(typeId);
            if (def == null) continue;

            int produceItemId = GameData.Instance.Items.GetNumericId(def.ProduceItemId);
            var baby = new AnimalEntity(map.AllocateEntityId())
            {
                Position = pos,
                Health = new Health(def.Health),
                Appearance = new TileAppearance(def.GlyphId, def.FgColor),
                AnimalData = new AnimalData
                {
                    AnimalTypeId = typeId,
                    ProduceItemTypeId = produceItemId,
                    ProduceIntervalTicks = def.ProduceIntervalTicks,
                    ProduceTicksCurrent = 0,
                    IsFed = false,
                    FedTicksRemaining = 0,
                    BreedCooldownTicks = def.BreedCooldownTicks,
                    BreedCooldownCurrent = def.BreedCooldownTicks,
                },
                AI = new AIState { StateId = AIStates.Idle },
                MoveDelay = new MoveDelay(5),
            };

            var chunk = map.GetChunkForWorldPos(pos);
            chunk?.AddEntity(baby);
        }
    }

    private static Position FindAdjacentDropPosition(WorldMap map, Position origin)
    {
        ReadOnlySpan<(int, int)> offsets = [(0, -1), (0, 1), (-1, 0), (1, 0), (-1, -1), (1, -1), (-1, 1), (1, 1)];
        foreach (var (dx, dy) in offsets)
        {
            var pos = Position.FromCoords(origin.X + dx, origin.Y + dy, origin.Z);
            if (map.IsWalkable(pos) && !map.IsPositionOccupiedByEntity(pos))
                return pos;
        }
        return origin;
    }
}
