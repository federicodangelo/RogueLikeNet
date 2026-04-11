using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Tests;

public class ChunkTests
{
    [Fact]
    public void Chunk_HasCorrectSize()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        Assert.Equal(64, Chunk.Size);
        Assert.Equal(64, chunk.Tiles.GetLength(0));
        Assert.Equal(64, chunk.Tiles.GetLength(1));
    }

    [Fact]
    public void InBounds_ReturnsTrueForValidCoords()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        Assert.True(chunk.InBounds(0, 0));
        Assert.True(chunk.InBounds(63, 63));
        Assert.False(chunk.InBounds(-1, 0));
        Assert.False(chunk.InBounds(0, 64));
    }

    [Fact]
    public void WorldToChunkCoord_PositiveCoords()
    {
        var (cx, cy, _) = Chunk.WorldToChunkCoord(Position.FromCoords(65, 130, Position.DefaultZ));
        Assert.Equal(1, cx);
        Assert.Equal(2, cy);
    }

    [Fact]
    public void WorldToChunkCoord_NegativeCoords()
    {
        var (cx, cy, _) = Chunk.WorldToChunkCoord(Position.FromCoords(-1, -64, Position.DefaultZ));
        Assert.Equal(-1, cx);
        Assert.Equal(-1, cy);
    }

    [Fact]
    public void WorldToLocal_ConvertsCorrectly()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(1, 0, Position.DefaultZ));
        Assert.True(chunk.WorldToLocal(64, 0, out int lx, out int ly));
        Assert.Equal(0, lx);
        Assert.Equal(0, ly);
    }

    [Fact]
    public void PackChunkKey_UniquePerCoord()
    {
        long k1 = Position.PackCoord(0, 0, Position.DefaultZ);
        long k2 = Position.PackCoord(1, 0, Position.DefaultZ);
        long k3 = Position.PackCoord(0, 1, Position.DefaultZ);
        Assert.NotEqual(k1, k2);
        Assert.NotEqual(k1, k3);
        Assert.NotEqual(k2, k3);
    }

    // ── ChunkPosition ──

    [Fact]
    public void ChunkPosition_Unpack_Roundtrip()
    {
        var original = ChunkPosition.FromCoords(3, -2, 5);
        var packed = original.Pack();
        var unpacked = new ChunkPosition();
        unpacked.Unpack(packed);
        Assert.Equal(original.X, unpacked.X);
        Assert.Equal(original.Y, unpacked.Y);
        Assert.Equal(original.Z, unpacked.Z);
    }

    [Fact]
    public void ChunkPosition_PackCoord_InstanceOverload()
    {
        var cp = ChunkPosition.FromCoords(1, 2, 3);
        Assert.Equal(ChunkPosition.PackCoord(1, 2, 3), ChunkPosition.PackCoord(cp));
    }

    [Fact]
    public void ChunkPosition_UnpackCoord_Roundtrip()
    {
        var original = ChunkPosition.FromCoords(5, -3, Position.DefaultZ);
        var packed = ChunkPosition.PackCoord(original);
        var unpacked = ChunkPosition.UnpackCoord(packed);
        Assert.Equal(original.X, unpacked.X);
        Assert.Equal(original.Y, unpacked.Y);
        Assert.Equal(original.Z, unpacked.Z);
    }

    // ── LocalToWorld ──

    [Fact]
    public void LocalToWorld_ConvertsCorrectly()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(1, 2, Position.DefaultZ));
        var worldPos = chunk.LocalToWorld(5, 10);
        Assert.Equal(Chunk.Size * 1 + 5, worldPos.X);
        Assert.Equal(Chunk.Size * 2 + 10, worldPos.Y);
        Assert.Equal(Position.DefaultZ, worldPos.Z);
    }

    // ── Entity add/remove/get ──

    [Fact]
    public void AddAndGetGroundItemRef()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var item = new GroundItemEntity(1) { Position = Position.FromCoords(5, 5, Position.DefaultZ), Item = new ItemData { ItemTypeId = 10 } };
        chunk.AddEntity(item);
        Assert.Equal(1, chunk.GroundItems.Length);
        ref var got = ref chunk.GetGroundItemRef(1);
        Assert.Equal(10, got.Item.ItemTypeId);
    }

    [Fact]
    public void GetGroundItemRef_Throws_WhenNotFound()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        Assert.Throws<KeyNotFoundException>(() => chunk.GetGroundItemRef(999));
    }

    [Fact]
    public void AddAndGetResourceNodeRef()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var node = new ResourceNodeEntity(1) { Position = Position.FromCoords(5, 5, Position.DefaultZ), Health = new Health(5) };
        chunk.AddEntity(node);
        ref var got = ref chunk.GetResourceNodeRef(1);
        Assert.Equal(5, got.Health.Current);
    }

    [Fact]
    public void GetResourceNodeRef_Throws_WhenNotFound()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        Assert.Throws<KeyNotFoundException>(() => chunk.GetResourceNodeRef(999));
    }

    [Fact]
    public void AddAndGetCropRef()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.AddEntity(new CropEntity(1) { Position = Position.FromCoords(5, 5, Position.DefaultZ) });
        ref var got = ref chunk.GetCropRef(1);
        Assert.Equal(1, got.Id);
    }

    [Fact]
    public void GetCropRef_Throws_WhenNotFound()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        Assert.Throws<KeyNotFoundException>(() => chunk.GetCropRef(999));
    }

    [Fact]
    public void AddAndGetAnimalRef()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.AddEntity(new AnimalEntity(1) { Position = Position.FromCoords(5, 5, Position.DefaultZ), Health = new Health(10) });
        ref var got = ref chunk.GetAnimalRef(1);
        Assert.Equal(10, got.Health.Current);
    }

    [Fact]
    public void GetAnimalRef_Throws_WhenNotFound()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        Assert.Throws<KeyNotFoundException>(() => chunk.GetAnimalRef(999));
    }

    [Fact]
    public void AddAndGetTownNpcRef()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.AddEntity(new TownNpcEntity(1) { Position = Position.FromCoords(5, 5, Position.DefaultZ), Health = new Health(50) });
        ref var got = ref chunk.GetTownNpcRef(1);
        Assert.Equal(50, got.Health.Current);
    }

    [Fact]
    public void GetTownNpcRef_Throws_WhenNotFound()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        Assert.Throws<KeyNotFoundException>(() => chunk.GetTownNpcRef(999));
    }

    // ── RemoveEntity by EntityRef ──

    [Fact]
    public void RemoveEntity_ByRef_Monster()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.AddEntity(new MonsterEntity(1) { Position = Position.FromCoords(0, 0, Position.DefaultZ), Health = new Health(10), MonsterData = new MonsterData { MonsterTypeId = 1 } });
        chunk.ClearSaveFlag();
        chunk.RemoveEntity(new EntityRef(1, EntityType.Monster));
        Assert.Equal(0, chunk.Monsters.Length);
        Assert.True(chunk.IsModifiedSinceLastSave);
    }

    [Fact]
    public void RemoveEntity_ByRef_GroundItem()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.AddEntity(new GroundItemEntity(1) { Position = Position.FromCoords(0, 0, Position.DefaultZ) });
        chunk.RemoveEntity(new EntityRef(1, EntityType.GroundItem));
        Assert.Equal(0, chunk.GroundItems.Length);
    }

    [Fact]
    public void RemoveEntity_ByRef_ResourceNode()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.AddEntity(new ResourceNodeEntity(1) { Position = Position.FromCoords(0, 0, Position.DefaultZ), Health = new Health(5) });
        chunk.RemoveEntity(new EntityRef(1, EntityType.ResourceNode));
        Assert.Equal(0, chunk.ResourceNodes.Length);
    }

    [Fact]
    public void RemoveEntity_ByRef_TownNpc()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.AddEntity(new TownNpcEntity(1) { Position = Position.FromCoords(0, 0, Position.DefaultZ), Health = new Health(50) });
        chunk.RemoveEntity(new EntityRef(1, EntityType.TownNpc));
        Assert.Equal(0, chunk.TownNpcs.Length);
    }

    [Fact]
    public void RemoveEntity_ByRef_Crop()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.AddEntity(new CropEntity(1) { Position = Position.FromCoords(0, 0, Position.DefaultZ) });
        chunk.RemoveEntity(new EntityRef(1, EntityType.Crop));
        Assert.Equal(0, chunk.Crops.Length);
    }

    [Fact]
    public void RemoveEntity_ByRef_Animal()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.AddEntity(new AnimalEntity(1) { Position = Position.FromCoords(0, 0, Position.DefaultZ), Health = new Health(10) });
        chunk.RemoveEntity(new EntityRef(1, EntityType.Animal));
        Assert.Equal(0, chunk.Animals.Length);
    }

    // ── Typed RemoveEntity overloads ──

    [Fact]
    public void RemoveEntity_GroundItem_Typed()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var item = new GroundItemEntity(1) { Position = Position.FromCoords(0, 0, Position.DefaultZ) };
        chunk.AddEntity(item);
        chunk.RemoveEntity(item);
        Assert.Equal(0, chunk.GroundItems.Length);
    }

    [Fact]
    public void RemoveEntity_ResourceNode_Typed()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var node = new ResourceNodeEntity(1) { Position = Position.FromCoords(0, 0, Position.DefaultZ), Health = new Health(5) };
        chunk.AddEntity(node);
        chunk.RemoveEntity(node);
        Assert.Equal(0, chunk.ResourceNodes.Length);
    }

    [Fact]
    public void RemoveEntity_Crop_Typed()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var crop = new CropEntity(1) { Position = Position.FromCoords(0, 0, Position.DefaultZ) };
        chunk.AddEntity(crop);
        chunk.RemoveEntity(crop);
        Assert.Equal(0, chunk.Crops.Length);
    }

    [Fact]
    public void RemoveEntity_Animal_Typed()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var animal = new AnimalEntity(1) { Position = Position.FromCoords(0, 0, Position.DefaultZ), Health = new Health(10) };
        chunk.AddEntity(animal);
        chunk.RemoveEntity(animal);
        Assert.Equal(0, chunk.Animals.Length);
    }

    // ── ResetLight, ClearEntities, RemoveDeadOrDestroyedEntities ──

    [Fact]
    public void ResetLight_ClearsAllLightLevels()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.LightLevels[5, 5] = 100;
        chunk.ResetLight();
        Assert.Equal(0, chunk.LightLevels[5, 5]);
    }

    [Fact]
    public void ClearEntities_RemovesAll()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.AddEntity(new MonsterEntity(1) { Health = new Health(10), MonsterData = new MonsterData { MonsterTypeId = 1 } });
        chunk.AddEntity(new GroundItemEntity(2));
        chunk.AddEntity(new ResourceNodeEntity(3) { Health = new Health(5) });
        chunk.AddEntity(new TownNpcEntity(4) { Health = new Health(50) });
        chunk.AddEntity(new CropEntity(6));
        chunk.AddEntity(new AnimalEntity(7) { Health = new Health(10) });

        chunk.ClearEntities();

        Assert.Equal(0, chunk.Monsters.Length);
        Assert.Equal(0, chunk.GroundItems.Length);
        Assert.Equal(0, chunk.ResourceNodes.Length);
        Assert.Equal(0, chunk.TownNpcs.Length);
        Assert.Equal(0, chunk.Crops.Length);
        Assert.Equal(0, chunk.Animals.Length);
    }

    [Fact]
    public void RemoveDeadOrDestroyedEntities_RemovesOnlyDead()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.AddEntity(new MonsterEntity(1) { Health = new Health(10), MonsterData = new MonsterData { MonsterTypeId = 1 } });
        chunk.AddEntity(new MonsterEntity(2) { Health = new Health(0), MonsterData = new MonsterData { MonsterTypeId = 1 } }); // Dead
        chunk.AddEntity(new GroundItemEntity(3) { IsDestroyed = true }); // Destroyed
        chunk.AddEntity(new GroundItemEntity(4));
        chunk.AddEntity(new AnimalEntity(5) { Health = new Health(0) }); // Dead
        chunk.AddEntity(new AnimalEntity(6) { Health = new Health(10) });
        chunk.ClearSaveFlag();

        chunk.RemoveDeadOrDestroyedEntities();

        Assert.Equal(1, chunk.Monsters.Length);
        Assert.Equal(1, chunk.GroundItems.Length);
        Assert.Equal(1, chunk.Animals.Length);
        Assert.True(chunk.IsModifiedSinceLastSave);
    }

    [Fact]
    public void RemoveDeadOrDestroyedEntities_NoChanges_DoesNotMarkModified()
    {
        var chunk = new Chunk(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        chunk.AddEntity(new MonsterEntity(1) { Health = new Health(10), MonsterData = new MonsterData { MonsterTypeId = 1 } });
        chunk.ClearSaveFlag();

        chunk.RemoveDeadOrDestroyedEntities();

        // All entities alive, shouldn't mark modified
        Assert.False(chunk.IsModifiedSinceLastSave);
    }

}
