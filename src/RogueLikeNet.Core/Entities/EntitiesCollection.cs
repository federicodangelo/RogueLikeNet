namespace RogueLikeNet.Core.Entities;

using System.Runtime.InteropServices;

public partial class EntitiesCollection
{
    public Span<MonsterEntity> Monsters => CollectionsMarshal.AsSpan(_monsters);
    public Span<GroundItemEntity> GroundItems => CollectionsMarshal.AsSpan(_groundItems);
    public Span<ResourceNodeEntity> ResourceNodes => CollectionsMarshal.AsSpan(_resourceNodes);
    public Span<TownNpcEntity> TownNpcs => CollectionsMarshal.AsSpan(_townNpcs);
    public Span<CropEntity> Crops => CollectionsMarshal.AsSpan(_crops);
    public Span<AnimalEntity> Animals => CollectionsMarshal.AsSpan(_animals);

    private readonly List<MonsterEntity> _monsters = [];
    private readonly List<GroundItemEntity> _groundItems = [];
    private readonly List<ResourceNodeEntity> _resourceNodes = [];
    private readonly List<TownNpcEntity> _townNpcs = [];
    private readonly List<CropEntity> _crops = [];
    private readonly List<AnimalEntity> _animals = [];

    public void RemoveEntity(EntityRef entity)
    {
        switch (entity.Type)
        {
            case EntityType.Monster:
                _monsters.RemoveAll(m => m.Id == entity.Id);
                OnModified();
                break;
            case EntityType.GroundItem:
                _groundItems.RemoveAll(i => i.Id == entity.Id);
                OnModified();
                break;
            case EntityType.ResourceNode:
                _resourceNodes.RemoveAll(r => r.Id == entity.Id);
                OnModified();
                break;
            case EntityType.TownNpc:
                _townNpcs.RemoveAll(n => n.Id == entity.Id);
                OnModified();
                break;
            case EntityType.Crop:
                _crops.RemoveAll(c => c.Id == entity.Id);
                OnModified();
                break;
            case EntityType.Animal:
                _animals.RemoveAll(a => a.Id == entity.Id);
                OnModified();
                break;
        }
    }

    public void RemoveEntity(MonsterEntity entity) { OnModified(); _monsters.RemoveAll(m => m.Id == entity.Id); }
    public void RemoveEntity(GroundItemEntity entity) { OnModified(); _groundItems.RemoveAll(i => i.Id == entity.Id); }
    public void RemoveEntity(ResourceNodeEntity entity) { OnModified(); _resourceNodes.RemoveAll(r => r.Id == entity.Id); }
    public void RemoveEntity(TownNpcEntity entity) { OnModified(); _townNpcs.RemoveAll(n => n.Id == entity.Id); }
    public void RemoveEntity(CropEntity entity) { OnModified(); _crops.RemoveAll(c => c.Id == entity.Id); }
    public void RemoveEntity(AnimalEntity entity) { OnModified(); _animals.RemoveAll(a => a.Id == entity.Id); }

    public ref MonsterEntity AddEntity(MonsterEntity entity) { OnModified(); _monsters.Add(entity); return ref Monsters[^1]; }
    public ref GroundItemEntity AddEntity(GroundItemEntity entity) { OnModified(); _groundItems.Add(entity); return ref GroundItems[^1]; }
    public ref ResourceNodeEntity AddEntity(ResourceNodeEntity entity) { OnModified(); _resourceNodes.Add(entity); return ref ResourceNodes[^1]; }
    public ref TownNpcEntity AddEntity(TownNpcEntity entity) { OnModified(); _townNpcs.Add(entity); return ref TownNpcs[^1]; }
    public ref CropEntity AddEntity(CropEntity entity) { OnModified(); _crops.Add(entity); return ref Crops[^1]; }
    public ref AnimalEntity AddEntity(AnimalEntity entity) { OnModified(); _animals.Add(entity); return ref Animals[^1]; }

    // ── Ref getters for in-place mutation ─────────────────────────────

    public ref MonsterEntity GetMonsterRef(int entityId)
    {
        var span = Monsters;
        for (int i = 0; i < span.Length; i++)
            if (span[i].Id == entityId) return ref span[i];
        throw new KeyNotFoundException($"Monster entity {entityId} not found in chunk.");
    }

    public ref GroundItemEntity GetGroundItemRef(int entityId)
    {
        var span = GroundItems;
        for (int i = 0; i < span.Length; i++)
            if (span[i].Id == entityId) return ref span[i];
        throw new KeyNotFoundException($"Ground item entity {entityId} not found in chunk.");
    }

    public ref ResourceNodeEntity GetResourceNodeRef(int entityId)
    {
        var span = ResourceNodes;
        for (int i = 0; i < span.Length; i++)
            if (span[i].Id == entityId) return ref span[i];
        throw new KeyNotFoundException($"Resource node entity {entityId} not found in chunk.");
    }

    public ref TownNpcEntity GetTownNpcRef(int entityId)
    {
        var span = TownNpcs;
        for (int i = 0; i < span.Length; i++)
            if (span[i].Id == entityId) return ref span[i];
        throw new KeyNotFoundException($"Town NPC entity {entityId} not found in chunk.");
    }

    public ref CropEntity GetCropRef(int entityId)
    {
        var span = Crops;
        for (int i = 0; i < span.Length; i++)
            if (span[i].Id == entityId) return ref span[i];
        throw new KeyNotFoundException($"Crop entity {entityId} not found in chunk.");
    }

    public ref AnimalEntity GetAnimalRef(int entityId)
    {
        var span = Animals;
        for (int i = 0; i < span.Length; i++)
            if (span[i].Id == entityId) return ref span[i];
        throw new KeyNotFoundException($"Animal entity {entityId} not found in chunk.");
    }

    /// <summary>Removes dead entities from all lists (compacts in-place).</summary>
    public int RemoveDeadOrDestroyedEntities()
    {
        int removedCount =
            _monsters.RemoveAll(m => m.IsDead) +
            _groundItems.RemoveAll(i => i.IsDestroyed) +
            _resourceNodes.RemoveAll(r => r.IsDead) +
            _townNpcs.RemoveAll(n => n.IsDead) +
            _crops.RemoveAll(c => c.IsDestroyed) +
            _animals.RemoveAll(a => a.IsDead);

        if (removedCount > 0)
            OnModified();

        return removedCount;
    }

    /// <summary>Clears all entity lists (used when unloading a chunk).</summary>
    public void ClearEntities()
    {
        _monsters.Clear();
        _groundItems.Clear();
        _resourceNodes.Clear();
        _townNpcs.Clear();
        _crops.Clear();
        _animals.Clear();
        OnModified();
    }

    protected virtual void OnModified() { }
}
