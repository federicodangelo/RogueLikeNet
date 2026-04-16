using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Entities;

public partial class EntitiesCollection
{
    public readonly ref struct EntityWithHealthAndPosition
    {
        public readonly EntityRef Entity;
        public readonly ref Position Position;
        public readonly ref Health Health;
        public readonly bool IsDead => !Health.IsAlive;

        public EntityWithHealthAndPosition(EntityRef entityRef, ref Position position, ref Health health)
        {
            Entity = entityRef;
            Position = ref position;
            Health = ref health;
        }
    }

    public ref struct EntitiesWithHealthAndPositionEnumerator
    {
        private readonly EntitiesCollection _collection;
        private int _phase;
        private int _index;

        public EntitiesWithHealthAndPositionEnumerator(EntitiesCollection collection)
        {
            _collection = collection;
            _phase = 0;
            _index = -1;
        }

        public readonly EntitiesWithHealthAndPositionEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            _index++;
            while (_phase <= 3)
            {
                var count = _phase switch
                {
                    0 => _collection._monsters.Count,
                    1 => _collection._resourceNodes.Count,
                    2 => _collection._townNpcs.Count,
                    3 => _collection._animals.Count,
                    _ => 0
                };
                if (_index < count) return true;
                _phase++;
                _index = 0;
            }
            return false;
        }

        public readonly EntityWithHealthAndPosition Current => _phase switch
        {
            0 => new EntityWithHealthAndPosition(
                new EntityRef(_collection._monsters[_index].Id, EntityType.Monster),
                ref _collection.Monsters[_index].Position,
                ref _collection.Monsters[_index].Health),
            1 => new EntityWithHealthAndPosition(
                new EntityRef(_collection._resourceNodes[_index].Id, EntityType.ResourceNode),
                ref _collection.ResourceNodes[_index].Position,
                ref _collection.ResourceNodes[_index].Health),
            2 => new EntityWithHealthAndPosition(
                new EntityRef(_collection._townNpcs[_index].Id, EntityType.TownNpc),
                ref _collection.TownNpcs[_index].Position,
                ref _collection.TownNpcs[_index].Health),
            3 => new EntityWithHealthAndPosition(
                new EntityRef(_collection._animals[_index].Id, EntityType.Animal),
                ref _collection.Animals[_index].Position,
                ref _collection.Animals[_index].Health),
            _ => throw new InvalidOperationException()
        };
    }

    public EntitiesWithHealthAndPositionEnumerator AllEntitiesWithHealthAndPosition => new(this);

    public readonly ref struct EntityWithPosition
    {
        public readonly EntityRef Entity;
        public readonly ref Position Position;
        public readonly ref TileAppearance Appearance;
        public readonly bool IsDeadOrDestroyed;

        public EntityWithPosition(EntityRef entityRef, ref Position position, bool isDeadOrDestroyed)
        {
            Entity = entityRef;
            Position = ref position;
            IsDeadOrDestroyed = isDeadOrDestroyed;
        }
    }

    public ref struct EntitiesWithPositionEnumerator
    {
        private readonly EntitiesCollection _collection;
        private int _phase;
        private int _index;

        public EntitiesWithPositionEnumerator(EntitiesCollection collection)
        {
            _collection = collection;
            _phase = 0;
            _index = -1;
        }

        public readonly EntitiesWithPositionEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            _index++;
            while (_phase <= 5)
            {
                var count = _phase switch
                {
                    0 => _collection._monsters.Count,
                    1 => _collection._resourceNodes.Count,
                    2 => _collection._townNpcs.Count,
                    3 => _collection._animals.Count,
                    4 => _collection._groundItems.Count,
                    5 => _collection._crops.Count,
                    _ => 0
                };

                if (_index < count) return true;
                _phase++;
                _index = 0;
            }

            return false;
        }

        public readonly EntityWithPosition Current => _phase switch
        {
            0 => new EntityWithPosition(
                new EntityRef(_collection._monsters[_index].Id, EntityType.Monster),
                ref _collection.Monsters[_index].Position,
                _collection.Monsters[_index].IsDead),
            1 => new EntityWithPosition(
                new EntityRef(_collection._resourceNodes[_index].Id, EntityType.ResourceNode),
                ref _collection.ResourceNodes[_index].Position,
                _collection.ResourceNodes[_index].IsDead),
            2 => new EntityWithPosition(
                new EntityRef(_collection._townNpcs[_index].Id, EntityType.TownNpc),
                ref _collection.TownNpcs[_index].Position,
                _collection.TownNpcs[_index].IsDead),
            3 => new EntityWithPosition(
                new EntityRef(_collection._animals[_index].Id, EntityType.Animal),
                ref _collection.Animals[_index].Position,
                _collection.Animals[_index].IsDead),
            4 => new EntityWithPosition(
                new EntityRef(_collection._groundItems[_index].Id, EntityType.GroundItem),
                ref _collection.GroundItems[_index].Position,
                _collection.GroundItems[_index].IsDestroyed),
            5 => new EntityWithPosition(
                new EntityRef(_collection._crops[_index].Id, EntityType.Crop),
                ref _collection.Crops[_index].Position,
                _collection.Crops[_index].IsDestroyed),
            _ => throw new InvalidOperationException()
        };
    }

    public EntitiesWithPositionEnumerator AllEntitiesWithPosition => new(this);
}
