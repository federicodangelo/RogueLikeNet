using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.Utilities;
using RogueLikeNet.Core.World;
using RogueLikeNet.Protocol.Messages;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Protocol;

/// <summary>
/// Shared helpers for building snapshot/delta/HUD messages from game state.
/// </summary>
public static class GameStateSerializer
{
    public static ChunkDataMsg SerializeChunk(Chunk chunk, int serverPlayerId)
    {
        int total = Chunk.Size * Chunk.Size;

        byte[]? exploredTiles = null;
        if (serverPlayerId != 0)
            chunk.ServerExploredTilesByServerPlayerId?.TryGetValue(serverPlayerId, out exploredTiles);

        var msg = new ChunkDataMsg
        {
            ChunkX = chunk.ChunkPosition.X,
            ChunkY = chunk.ChunkPosition.Y,
            ChunkZ = chunk.ChunkPosition.Z,
            TileIds = new int[total],
            TilePlaceableItemIds = new int[total],
            TilePlaceableItemExtras = new int[total],
            ExploredTiles = exploredTiles,
        };

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                int idx = y * Chunk.Size + x;
                ref var tile = ref chunk.Tiles[x, y];
                msg.TileIds[idx] = tile.TileId;
                msg.TilePlaceableItemIds[idx] = tile.PlaceableItemId;
                msg.TilePlaceableItemExtras[idx] = tile.PlaceableItemExtra;
            }

        return msg;
    }

    public static ChunkDataMsg[] SerializeChunksAroundPosition(GameEngine engine, Position pos, int serverPlayerId)
    {
        var chunkPos = Chunk.WorldToChunkCoord(pos);
        var chunks = new List<ChunkDataMsg>();
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var chunk = engine.EnsureChunkLoaded(ChunkPosition.FromCoords(chunkPos.X + dx, chunkPos.Y + dy, chunkPos.Z));
                chunks.Add(SerializeChunk(chunk, serverPlayerId));
            }
        return chunks.ToArray();
    }

    /// <summary>
    /// Delta-aware chunk serialization using an LRU tracker.
    /// Sends full ChunkDataMsg only for new chunks. Already-tracked chunks are
    /// touched (promoted in LRU). After adding new chunks, the tracker evicts
    /// least-recently-used entries beyond its capacity, returning their keys so
    /// the client can discard them.
    /// </summary>
    public static ChunkDeltaResult SerializeChunksDelta(
        GameEngine engine, Position pos, ChunkTracker tracker,
        int visibleChunks, int serverPlayerId, int maxChunksToSerialize = int.MaxValue)
    {
        tracker.UpdateCapacity(visibleChunks);
        var chunkRange = ChunkTracker.ComputeChunkRange(visibleChunks);
        var chunkPos = Chunk.WorldToChunkCoord(pos);
        var newChunks = new List<ChunkDataMsg>();

        foreach (var z in PointsAtDistance.GetZLevels(1))
        {
            int ccz = chunkPos.Z + z;
            if (ccz < 0 || ccz > 255) continue;
            foreach (var point in PointsAtDistance.GetPoints(chunkRange))
            {
                int ccx = chunkPos.X + point.X, ccy = chunkPos.Y + point.Y;
                long key = ChunkPosition.PackCoord(ccx, ccy, ccz);

                if (tracker.Touch(key))
                {
                    var chunk = engine.EnsureChunkLoadedOrDoesntExist(ChunkPosition.FromCoords(ccx, ccy, ccz));
                    if (chunk != null)
                    {
                        newChunks.Add(SerializeChunk(chunk, serverPlayerId));
                        maxChunksToSerialize--;
                        if (maxChunksToSerialize <= 0)
                            break;
                    }
                }
            }
        }

        var discarded = tracker.Evict();

        return new ChunkDeltaResult(newChunks.ToArray(), discarded);
    }

    /// <summary>
    /// Builds a delta-compressed entity update for a specific player.
    /// Only includes entities visible to the player's FOV, and marks
    /// previously-visible entities that left FOV as removed.
    /// Returns updated previousState for the next tick.
    /// </summary>
    public static SerializedEntityData SerializeEntityDelta(WorldMap map, FOVData fov,
            Dictionary<long, EntityUpdateMsg> previousState, bool debugVisibilityOff = false)
    {
        var fullUpdates = new List<EntityUpdateMsg>();
        var positionHealthUpdates = new List<EntityPositionHealthMsg>();
        var removals = new List<EntityRemovedMsg>();
        var currentIds = new HashSet<long>();

        void ProcessEntity(long id, EntityType entityType, Position pos, TileAppearance appearance,
            int hp, int maxHp, int lightRadius, ItemDataMsg? item, int typeNumericId = 0)
        {
            if (!debugVisibilityOff && !fov.IsVisible(pos)) return;

            currentIds.Add(id);

            var current = new EntityUpdateMsg
            {
                Id = id,
                EntityType = (int)entityType,
                X = pos.X,
                Y = pos.Y,
                Z = pos.Z,
                GlyphId = appearance.GlyphId,
                FgColor = appearance.FgColor,
                Health = hp,
                MaxHealth = maxHp,
                LightRadius = lightRadius,
                Item = item,
                TypeNumericId = typeNumericId,
            };

            if (previousState.TryGetValue(id, out var prev))
            {
                if (!current.SameValues(prev))
                {
                    if (current.HasOnlyPositionHealthChanges(prev))
                        positionHealthUpdates.Add(new EntityPositionHealthMsg { Id = id, X = pos.X, Y = pos.Y, Z = pos.Z, Health = hp });
                    else
                        fullUpdates.Add(current);
                }
            }
            else
            {
                fullUpdates.Add(current);
            }

            previousState[id] = current;
        }

        // Players
        foreach (var p in map.Players)
        {
            if (p.IsDead) continue;
            ProcessEntity((long)p.Id, EntityType.Player, p.Position, p.Appearance, p.Health.Current, p.Health.Max, 0, null);
        }

        // Iterate loaded chunks
        foreach (var chunk in map.LoadedChunks)
        {
            foreach (var m in chunk.Monsters)
            {
                if (m.IsDead) continue;
                ProcessEntity((long)m.Id, EntityType.Monster, m.Position, m.Appearance, m.Health.Current, m.Health.Max, 0, null, m.MonsterData.MonsterTypeId);
            }

            foreach (var gi in chunk.GroundItems)
            {
                if (gi.IsDestroyed) continue;
                var item = new ItemDataMsg
                {
                    ItemTypeId = gi.Item.ItemTypeId,
                    StackCount = gi.Item.StackCount,
                };
                ProcessEntity((long)gi.Id, EntityType.GroundItem, gi.Position, gi.Appearance, 0, 0, 0, item);
            }

            foreach (var r in chunk.ResourceNodes)
            {
                if (r.IsDead) continue;
                ProcessEntity((long)r.Id, EntityType.ResourceNode, r.Position, r.Appearance, r.Health.Current, r.Health.Max, 0, null, r.NodeData.NodeTypeId);
            }

            foreach (var n in chunk.TownNpcs)
            {
                if (n.IsDead) continue;
                ProcessEntity((long)n.Id, EntityType.TownNpc, n.Position, n.Appearance, n.Health.Current, n.Health.Max, 0, null);
            }

            foreach (var c in chunk.Crops)
            {
                if (c.IsDestroyed) continue;
                ProcessEntity((long)c.Id, EntityType.Crop, c.Position, c.Appearance, 0, 0, 0, null);
            }

            foreach (var a in chunk.Animals)
            {
                if (a.IsDead) continue;
                ProcessEntity((long)a.Id, EntityType.Animal, a.Position, a.Appearance, a.Health.Current, a.Health.Max, 0, null);
            }
        }

        // Entities that were visible last tick but are no longer → removed
        var staleIds = previousState.Keys.Where(id => !currentIds.Contains(id)).ToList();
        foreach (var id in staleIds)
        {
            removals.Add(new EntityRemovedMsg { Id = id });
            previousState.Remove(id);
        }

        return new SerializedEntityData(fullUpdates.ToArray(), positionHealthUpdates.ToArray(), removals.ToArray());
    }

    public static CombatEventMsg[] SerializeCombatEvents(GameEngine engine)
    {
        var combatEvents = engine.Combat.LastTickEvents;
        var spellEvents = engine.Spells.LastTickEvents;
        if (combatEvents.Count == 0 && spellEvents.Count == 0) return [];

        static CombatEventMsg ToMsg(CombatEvent e) => new()
        {
            AttackerX = e.Attacker.X,
            AttackerY = e.Attacker.Y,
            TargetX = e.Target.X,
            TargetY = e.Target.Y,
            Damage = e.Damage,
            TargetDied = e.TargetDied,
            Blocked = e.Blocked,
            IsRanged = e.IsRanged,
            DamageType = (int)e.DamageType,
            WasResisted = e.WasResisted,
            WasWeakness = e.WasWeakness,
        };

        var result = new CombatEventMsg[combatEvents.Count + spellEvents.Count];
        int idx = 0;
        foreach (var e in combatEvents)
            result[idx++] = ToMsg(e);
        foreach (var e in spellEvents)
            result[idx++] = ToMsg(e);
        return result;
    }

    public static NpcInteractionMsg[] SerializeNpcInteractions(GameEngine engine, PlayerEntity player)
    {
        var events = engine.Combat.LastTickInteractionEvents;
        if (events.Count == 0) return [];
        var result = new List<NpcInteractionMsg>();
        foreach (var e in events)
        {
            if (e.PlayerEntityId != player.Id) continue;
            result.Add(BuildNpcInteractionMsg(e, player));
        }
        return result.ToArray();
    }

    private static NpcInteractionMsg BuildNpcInteractionMsg(NpcInteractionEvent e, PlayerEntity player)
    {
        var offers = new QuestOfferMsg[e.OfferedQuestIds?.Length ?? 0];
        for (int i = 0; i < offers.Length; i++)
        {
            var q = GameData.Instance.Quests.Get(e.OfferedQuestIds![i]);
            if (q == null) { offers[i] = new QuestOfferMsg { QuestNumericId = e.OfferedQuestIds[i] }; continue; }
            offers[i] = new QuestOfferMsg
            {
                QuestNumericId = q.NumericId,
                QuestStringId = q.Id ?? "",
                Title = q.Title,
                Description = q.Description,
                Objectives = BuildObjectivePreview(q),
                Rewards = BuildRewardInfo(q),
            };
        }

        var turnIns = new QuestTurnInMsg[e.TurnInQuestIds?.Length ?? 0];
        for (int i = 0; i < turnIns.Length; i++)
        {
            int qid = e.TurnInQuestIds![i];
            var q = GameData.Instance.Quests.Get(qid);
            var active = player.Quests.GetActive(qid);
            turnIns[i] = new QuestTurnInMsg
            {
                QuestNumericId = qid,
                Title = q?.Title ?? "",
                CompletionText = q?.CompletionText ?? "",
                IsComplete = active?.AllObjectivesComplete ?? false,
                Objectives = BuildObjectiveProgress(q, active),
                Rewards = q != null ? BuildRewardInfo(q) : null,
            };
        }

        return new NpcInteractionMsg
        {
            NpcEntityId = e.NpcEntityId,
            NpcX = e.Npc.X,
            NpcY = e.Npc.Y,
            NpcZ = e.Npc.Z,
            NpcName = e.NpcName ?? "",
            NpcRole = e.NpcRole,
            FlavorText = e.Text ?? "",
            QuestOffers = offers,
            QuestTurnIns = turnIns,
            HasShop = e.HasShop,
        };
    }

    private static QuestObjectiveInfoMsg[] BuildObjectivePreview(QuestDefinition q)
    {
        var arr = new QuestObjectiveInfoMsg[q.Objectives.Length];
        for (int i = 0; i < q.Objectives.Length; i++)
        {
            var o = q.Objectives[i];
            arr[i] = new QuestObjectiveInfoMsg
            {
                Type = (int)o.Type,
                TargetNumericId = o.TargetNumericId,
                Current = 0,
                Target = o.Count,
                Description = o.Description ?? "",
            };
        }
        return arr;
    }

    private static QuestObjectiveInfoMsg[] BuildObjectiveProgress(QuestDefinition? q, ActiveQuest? active)
    {
        if (q == null) return [];
        var arr = new QuestObjectiveInfoMsg[q.Objectives.Length];
        for (int i = 0; i < q.Objectives.Length; i++)
        {
            var o = q.Objectives[i];
            int current = active != null && i < active.Objectives.Length ? active.Objectives[i].Current : 0;
            arr[i] = new QuestObjectiveInfoMsg
            {
                Type = (int)o.Type,
                TargetNumericId = o.TargetNumericId,
                Current = current,
                Target = o.Count,
                Description = o.Description ?? "",
            };
        }
        return arr;
    }

    private static QuestRewardInfoMsg BuildRewardInfo(QuestDefinition q)
    {
        var items = new ItemDataMsg[q.Rewards.Items.Length];
        for (int i = 0; i < q.Rewards.Items.Length; i++)
        {
            items[i] = new ItemDataMsg
            {
                ItemTypeId = q.Rewards.Items[i].ItemNumericId,
                StackCount = q.Rewards.Items[i].Count,
                EquipSlot = -1,
            };
        }
        return new QuestRewardInfoMsg
        {
            Experience = q.Rewards.Experience,
            Gold = q.Rewards.Gold,
            Items = items,
        };
    }

    public static PlayerActionEventMsg[] SerializePlayerActionEvents(PlayerEntity player)
    {
        if (player.ActionEvents.Count == 0) return [];
        return player.ActionEvents.Select(e => new PlayerActionEventMsg
        {
            EventType = (int)e.EventType,
            ItemTypeId = e.ItemTypeId,
            StackCount = e.StackCount,
            Failed = e.Failed,
            FailReason = (int)e.FailReason,
            OldLevel = e.OldLevel,
            NewLevel = e.NewLevel,
            KilledNpcTypeId = (int)e.KilledNpcTypeId,
            QuestNumericId = e.QuestNumericId,
            QuestObjectiveIndex = e.QuestObjectiveIndex,
            ObjectiveCurrent = e.ObjectiveCurrent,
            ObjectiveTarget = e.ObjectiveTarget,
        }).ToArray();
    }

    private static PlayerQuestStateMsg BuildQuestState(GameEngine engine, PlayerEntity player)
    {
        var active = player.Quests.ActiveQuests ?? new List<ActiveQuest>();
        var completed = player.Quests.CompletedQuestIds ?? new List<int>();
        var activeArr = new ActiveQuestInfoMsg[active.Count];
        for (int i = 0; i < active.Count; i++)
        {
            var aq = active[i];
            var q = GameData.Instance.Quests.Get(aq.QuestNumericId);
            var objs = new QuestObjectiveInfoMsg[aq.Objectives.Length];
            for (int j = 0; j < aq.Objectives.Length; j++)
            {
                var defObj = q != null && j < q.Objectives.Length ? q.Objectives[j] : null;
                objs[j] = new QuestObjectiveInfoMsg
                {
                    Type = defObj != null ? (int)defObj.Type : 0,
                    TargetNumericId = defObj?.TargetNumericId ?? 0,
                    Current = aq.Objectives[j].Current,
                    Target = aq.Objectives[j].Target,
                    Description = defObj?.Description ?? "",
                };
            }
            activeArr[i] = new ActiveQuestInfoMsg
            {
                QuestNumericId = aq.QuestNumericId,
                Title = q?.Title ?? "",
                GiverEntityId = aq.GiverEntityId,
                GiverName = aq.GiverName,
                TownX = aq.TownX,
                TownY = aq.TownY,
                TownZ = aq.TownZ,
                Objectives = objs,
            };
        }
        return new PlayerQuestStateMsg
        {
            Active = activeArr,
            CompletedQuestIds = completed.ToArray(),
            QuestGiverEntityIds = BuildQuestGiverEntityIds(engine, player),
        };
    }

    private static int[] BuildQuestGiverEntityIds(GameEngine engine, PlayerEntity player)
    {
        var list = new List<int>();
        foreach (var chunk in engine.WorldMap.LoadedChunks)
        {
            foreach (ref var npc in chunk.TownNpcs)
            {
                if (npc.IsDead) continue;
                if (QuestSystem.HasAvailableOfferForRole(ref player, npc.NpcData.Role))
                    list.Add(npc.Id);
            }
        }
        return list.ToArray();
    }

    public static PlayerStateMsg? BuildPlayerState(GameEngine engine, PlayerEntity player)
    {
        var stateData = engine.GetPlayerStateData(player);
        if (stateData == null) return null;
        return new PlayerStateMsg
        {
            Health = stateData.Health,
            MaxHealth = stateData.MaxHealth,
            Attack = stateData.Attack,
            Defense = stateData.Defense,
            BonusAttack = stateData.BonusAttack,
            BonusDefense = stateData.BonusDefense,
            Level = stateData.Level,
            Experience = stateData.Experience,
            Hunger = stateData.Hunger,
            MaxHunger = stateData.MaxHunger,
            Thirst = stateData.Thirst,
            MaxThirst = stateData.MaxThirst,
            Mana = stateData.Mana,
            MaxMana = stateData.MaxMana,
            InventoryCount = stateData.InventoryCount,
            InventoryCapacity = stateData.InventoryCapacity,
            InventoryItems = stateData.InventoryItems.Select(i => new ItemDataMsg { ItemTypeId = i.ItemTypeId, StackCount = i.StackCount, EquipSlot = -1 }).ToArray(),
            EquippedItems = stateData.EquippedItems.Select(i => new ItemDataMsg { ItemTypeId = i.ItemTypeId, StackCount = i.StackCount, EquipSlot = i.EquipSlot }).ToArray(),
            QuickSlotIndices = stateData.QuickSlotIndices,
            PlayerEntityId = (long)player.Id,
            NearbyStationsTypes = stateData.NearbyStationsTypes,
            ClassId = stateData.ClassId,
            Quests = BuildQuestState(engine, player),
        };
    }

    public static PlayerStateMsg? SerializePlayerStateDelta(GameEngine engine, PlayerEntity player, PlayerStateMsg? lastSentPlayerState)
    {
        var playerState = BuildPlayerState(engine, player);

        if (playerState == null) return null;

        if (lastSentPlayerState != null && lastSentPlayerState.Equals(playerState))
            return null;

        return playerState;
    }
}

public readonly record struct SerializedEntityData(EntityUpdateMsg[] FullUpdates, EntityPositionHealthMsg[] PositionHealthUpdates, EntityRemovedMsg[] Removals);

public readonly record struct ChunkDeltaResult(ChunkDataMsg[] NewChunks, long[] DiscardedKeys);
