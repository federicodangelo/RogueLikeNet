using RogueLikeNet.Core.Algorithms;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Handles melee and ranged combat. All damage is integer.
/// Melee attacks auto-target the closest adjacent enemy (cardinal + same tile).
/// Ranged attacks auto-target the closest enemy within weapon range with line-of-sight.
/// Bumping a town NPC triggers dialogue instead of damage.
/// </summary>
public class CombatSystem
{
    private readonly List<CombatEvent> _events = new();
    private readonly List<NpcInteractionEvent> _interactionEvents = new();
    private readonly Random _rng = new();

    private static readonly (int DX, int DY)[] MeleeOffsets =
        [(0, 0), (0, -1), (0, 1), (-1, 0), (1, 0)];

    public IReadOnlyList<CombatEvent> LastTickEvents => _events;
    public IReadOnlyList<NpcInteractionEvent> LastTickInteractionEvents => _interactionEvents;

    public void Update(WorldMap map, bool debugInvulnerable = false)
    {
        _events.Clear();
        _interactionEvents.Clear();

        // Player attacks
        ProcessPlayerAttacks(map);

        // Monster attacks
        ProcessMonsterAttacks(map, debugInvulnerable);
    }

    private void ProcessPlayerAttacks(WorldMap map)
    {
        foreach (ref var player in map.Players)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType != ActionTypes.Attack) continue;
            if (player.AttackDelay.Current > 0) continue;

            int attackerAttack = player.CombatStats.Attack;

            // Determine weapon range
            int weaponRange = 1;
            bool isRanged = false;
            bool usesAmmo = false;
            int ammoBonusDamage = 0;
            DamageType damageType = DamageType.Physical;
            var weaponItem = player.Equipment.Hand;
            ItemDefinition? weaponDef = null;
            if (!weaponItem.IsNone)
            {
                weaponDef = GameData.Instance.Items.Get(weaponItem.ItemTypeId);
                if (weaponDef?.Weapon != null)
                    damageType = weaponDef.Weapon.DamageType;
                if (weaponDef?.Weapon != null && weaponDef.Weapon.Range > 1)
                {
                    weaponRange = weaponDef.Weapon.Range;
                    isRanged = true;
                    usesAmmo = weaponDef.Weapon.UsesAmmo;
                }
            }

            // For ranged weapons that consume ammo, find and validate ammo
            int ammoSlot = -1;
            if (isRanged && usesAmmo)
            {
                ammoSlot = FindAmmoSlot(ref player);
                if (ammoSlot < 0)
                {
                    // No ammo — fall back to melee (base stats, range 1)
                    isRanged = false;
                    weaponRange = 1;
                    ammoBonusDamage = 0;
                }
                else
                {
                    var ammoItem = player.Inventory.Items[ammoSlot];
                    var ammoDef = GameData.Instance.Items.Get(ammoItem.ItemTypeId);
                    if (ammoDef?.Ammo != null)
                    {
                        ammoBonusDamage = ammoDef.Ammo.Damage;
                        damageType = ammoDef.Ammo.DamageType;
                    }
                }
            }

            var targetPosition = player.Position;
            if (player.Input.TargetX == 0 && player.Input.TargetY == 0)
            {
                Position? best;
                if (isRanged)
                    best = FindClosestRangedTarget(map, player, weaponRange);
                else
                    best = FindClosestAdjacentTarget(map, player);

                if (best == null)
                {
                    player.Input.ActionType = ActionTypes.None;
                    continue;
                }
                targetPosition = best.Value;
            }
            else
            {
                targetPosition.X += player.Input.TargetX;
                targetPosition.Y += player.Input.TargetY;
            }

            // For ranged attacks, verify line-of-sight to target
            if (isRanged && targetPosition != player.Position)
            {
                int dist = Math.Abs(targetPosition.X - player.Position.X)
                         + Math.Abs(targetPosition.Y - player.Position.Y);
                int playerZ = player.Position.Z;
                if (dist > weaponRange || !Bresenham.HasLineOfSight(
                    player.Position.X, player.Position.Y,
                    targetPosition.X, targetPosition.Y,
                    (x, y) => !map.IsTransparent(Position.FromCoords(x, y, playerZ))))
                {
                    player.Input.ActionType = ActionTypes.None;
                    continue;
                }
            }

            // Consume ammo for ranged attacks that require it
            if (isRanged && usesAmmo && ammoSlot >= 0)
            {
                ConsumeAmmo(ref player, ammoSlot);
            }

            // Total attack = base stats + ammo bonus damage for ranged
            int totalAttack = attackerAttack + ammoBonusDamage;

            // Check monsters and resource nodes at target position
            var targetChunk = map.GetChunkForWorldPos(targetPosition);
            if (targetChunk != null)
            {
                // Check Town NPCs first (interaction instead of damage)
                foreach (ref var npc in targetChunk.TownNpcs)
                {
                    if (npc.IsDead || npc.Position != targetPosition) continue;
                    if (npc.NpcData.TalkTimer <= 0)
                    {
                        npc.NpcData.TalkTimer = 60;
                        npc.NpcData.InConversationWith = player.Id;
                        string dialogue = TownNpcDefinitions.Dialogues[npc.NpcData.DialogueIndex];
                        npc.NpcData.DialogueIndex = (npc.NpcData.DialogueIndex + 1) % TownNpcDefinitions.Dialogues.Length;
                        _interactionEvents.Add(BuildInteractionEvent(ref player, ref npc, dialogue));
                    }
                }

                // Monsters
                foreach (ref var monster in targetChunk.Monsters)
                {
                    if (monster.IsDead || monster.Position != targetPosition) continue;

                    var damage = DamageResolver.ResolveAgainstNpc(
                        totalAttack,
                        monster.CombatStats.Defense,
                        damageType,
                        monster.MonsterData.MonsterTypeId);
                    monster.Health.Current = Math.Max(0, monster.Health.Current - damage.Damage);

                    var combatEvent = BuildCombatEvent(player.Position, monster.Position, damage, monster.IsDead, isRanged);
                    if (StatusEffectSystem.TryApplyFromDamageType(ref monster, damage.DamageType, player.Id, damage.Damage, out var appliedEffectType))
                        combatEvent.StatusEffectType = appliedEffectType;
                    _events.Add(combatEvent);

                    if (monster.IsDead)
                    {
                        player.ActionEvents.Add(new PlayerActionEvent
                        {
                            EventType = PlayerActionEventType.Kill,
                            KilledNpcTypeId = monster.MonsterData.MonsterTypeId,
                        });

                        if (HasNextLevel(ref player))
                        {
                            var npcDef = GameData.Instance.Npcs.Get(monster.MonsterData.MonsterTypeId);
                            if (npcDef != null)
                            {
                                player.ClassData.Experience += npcDef.XpReward;
                                ProcessLevelUp(ref player);
                            }
                        }
                    }
                }

                // Resource nodes — tools provide bonus damage (melee only)
                if (!isRanged)
                {
                    foreach (ref var node in targetChunk.ResourceNodes)
                    {
                        if (node.IsDead || node.Position != targetPosition) continue;

                        int effectiveAttack = attackerAttack;
                        int toolBonus = GetToolBonus(ref player, node.NodeData.RequiredToolType);
                        effectiveAttack += toolBonus;

                        var damage = DamageResolver.Resolve(effectiveAttack, node.CombatStats.Defense);
                        node.Health.Current = Math.Max(0, node.Health.Current - damage.Damage);

                        _events.Add(BuildCombatEvent(player.Position, node.Position, damage, node.IsDead));

                        if (node.IsDead)
                        {
                            player.ActionEvents.Add(new PlayerActionEvent
                            {
                                EventType = PlayerActionEventType.Gather,
                                ItemTypeId = node.NodeData.ResourceItemTypeId,
                                KilledNpcTypeId = node.NodeData.NodeTypeId,
                            });
                        }
                    }
                }
            }

            player.Input.ActionType = ActionTypes.None;
            player.AttackDelay.Current = player.AttackDelay.Interval;
        }
    }

    static private bool HasNextLevel(ref PlayerEntity player)
    {
        return player.ClassData.Level < GameData.Instance.PlayerLevels.MaxLevel;
    }

    private void ProcessLevelUp(ref PlayerEntity player)
    {
        var levelTable = GameData.Instance.PlayerLevels;
        int newLevel = levelTable.GetLevelForXp(player.ClassData.Experience);
        if (newLevel <= player.ClassData.Level) return;

        int oldLevel = player.ClassData.Level;
        player.ClassData.Level = newLevel;
        player.ClassData.Experience = 0;

        // Recalculate all stats from first principles
        ActiveEffectsSystem.RecalculatePlayerStats(ref player);

        // Full heal on level up
        player.Health.Current = player.Health.Max;
        player.Mana.Current = player.Mana.Max;

        player.ActionEvents.Add(new PlayerActionEvent
        {
            EventType = PlayerActionEventType.LevelUp,
            OldLevel = oldLevel,
            NewLevel = newLevel,
        });
    }

    private void ProcessMonsterAttacks(WorldMap map, bool debugInvulnerable)
    {
        if (debugInvulnerable) return;

        foreach (var chunk in map.LoadedChunks)
        {
            foreach (ref var monster in chunk.Monsters)
            {
                if (monster.IsDead || monster.AI.StateId != AIStates.Attack) continue;
                if (monster.AttackDelay.Current > 0) continue;

                foreach (ref var player in map.Players)
                {
                    if (player.IsDead) continue;

                    int dx = Math.Abs(monster.Position.X - player.Position.X);
                    int dy = Math.Abs(monster.Position.Y - player.Position.Y);
                    if (dx + dy > 1) continue;

                    // Shield in offhand adds block chance
                    int blockChance = GetShieldBlockChance(ref player);
                    if (blockChance > 0 && _rng.Next(100) < blockChance)
                    {
                        _events.Add(new CombatEvent
                        {
                            Attacker = monster.Position,
                            Target = player.Position,
                            Damage = 0,
                            TargetDied = false,
                            Blocked = true,
                        });
                        monster.AttackDelay.Current = monster.AttackDelay.Interval;
                        break;
                    }

                    var damage = DamageResolver.Resolve(monster.CombatStats.Attack, player.CombatStats.Defense);
                    player.Health.Current = Math.Max(0, player.Health.Current - damage.Damage);

                    _events.Add(BuildCombatEvent(monster.Position, player.Position, damage, player.IsDead));

                    monster.AttackDelay.Current = monster.AttackDelay.Interval;
                    break;
                }
            }
        }
    }

    private static Position? FindClosestAdjacentTarget(WorldMap map, PlayerEntity attacker)
    {
        Position? best = null;
        int bestDist = int.MaxValue;

        var chunk = map.GetChunkForWorldPos(attacker.Position);
        if (chunk == null) return null;

        // Check monsters and resource nodes at adjacent positions
        foreach (var monster in chunk.Monsters)
        {
            if (monster.IsDead) continue;
            int dx = monster.Position.X - attacker.Position.X;
            int dy = monster.Position.Y - attacker.Position.Y;
            bool adjacent = false;
            foreach (var (ox, oy) in MeleeOffsets)
                if (dx == ox && dy == oy) { adjacent = true; break; }
            if (!adjacent) continue;
            int dist = Math.Abs(dx) + Math.Abs(dy);
            if (dist < bestDist) { bestDist = dist; best = monster.Position; }
        }

        foreach (var node in chunk.ResourceNodes)
        {
            if (node.IsDead) continue;
            int dx = node.Position.X - attacker.Position.X;
            int dy = node.Position.Y - attacker.Position.Y;
            bool adjacent = false;
            foreach (var (ox, oy) in MeleeOffsets)
                if (dx == ox && dy == oy) { adjacent = true; break; }
            if (!adjacent) continue;
            int dist = Math.Abs(dx) + Math.Abs(dy);
            if (dist < bestDist) { bestDist = dist; best = node.Position; }
        }

        return best;
    }

    /// <summary>
    /// Finds the closest monster within weapon range that has a clear line-of-sight.
    /// Searches all loaded chunks for monsters within Manhattan distance &lt;= range.
    /// </summary>
    private static Position? FindClosestRangedTarget(WorldMap map, PlayerEntity attacker, int range)
    {
        Position? best = null;
        int bestDist = int.MaxValue;

        foreach (var chunk in map.LoadedChunks)
        {
            foreach (var monster in chunk.Monsters)
            {
                if (monster.IsDead) continue;
                if (monster.Position.Z != attacker.Position.Z) continue;

                int dx = Math.Abs(monster.Position.X - attacker.Position.X);
                int dy = Math.Abs(monster.Position.Y - attacker.Position.Y);
                int dist = dx + dy;
                if (dist > range || dist >= bestDist) continue;

                // Check line-of-sight
                if (!Bresenham.HasLineOfSight(
                    attacker.Position.X, attacker.Position.Y,
                    monster.Position.X, monster.Position.Y,
                    (x, y) => !map.IsTransparent(Position.FromCoords(x, y, attacker.Position.Z))))
                    continue;

                bestDist = dist;
                best = monster.Position;
            }
        }

        // Also check adjacent targets (melee fallback) — allows ranged weapons to hit at melee range
        if (best == null)
            best = FindClosestAdjacentTarget(map, attacker);

        return best;
    }

    /// <summary>
    /// Finds the first inventory slot containing ammo (ItemCategory.Ammo).
    /// Returns -1 if no ammo found.
    /// </summary>
    private static int FindAmmoSlot(ref PlayerEntity player)
    {
        var items = player.Inventory.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var itemDef = GameData.Instance.Items.Get(items[i].ItemTypeId);
            if (itemDef?.Category == ItemCategory.Ammo)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Consumes one ammo from the given inventory slot.
    /// Decrements StackCount; removes the slot if empty.
    /// </summary>
    private static void ConsumeAmmo(ref PlayerEntity player, int slot)
    {
        var items = player.Inventory.Items;
        var ammo = items[slot];
        ammo.StackCount--;
        if (ammo.StackCount <= 0)
            items.RemoveAt(slot);
        else
            items[slot] = ammo;
    }

    /// <summary>
    /// Calculates tool bonus damage when attacking a resource node.
    /// If the player has a tool equipped that matches the node's required tool type,
    /// the tool's MiningPower is added as bonus damage.
    /// </summary>
    private static int GetToolBonus(ref PlayerEntity player, ToolType requiredTool)
    {
        if (requiredTool == ToolType.None) return 0;

        // Check weapon slot for a matching tool
        var weaponItem = player.Equipment.Hand;
        if (weaponItem.IsNone) return 0;

        var itemDef = GameData.Instance.Items.Get(weaponItem.ItemTypeId);

        if (itemDef?.Tool != null && itemDef.Tool.ToolType == requiredTool)
            return itemDef.Tool.MiningPower;

        return 0;
    }

    /// <summary>
    /// Computes block chance from a shield in the offhand slot.
    /// Block chance = BaseDefense × MaterialTierMultiplier × 2 (%), capped at 50%.
    /// </summary>
    private static int GetShieldBlockChance(ref PlayerEntity player)
    {
        var offhand = player.Equipment.Offhand;
        if (offhand.IsNone) return 0;

        var def = GameData.Instance.Items.Get(offhand.ItemTypeId);
        if (def?.Armor == null || def.EquipSlot != EquipSlot.Offhand) return 0;

        int effectiveDef = MaterialTiers.Apply(def.Armor.BaseDefense, def.MaterialTier);
        return Math.Min(50, effectiveDef * 2);
    }

    /// <summary>
    /// Builds an <see cref="NpcInteractionEvent"/> for a player bumping the given NPC,
    /// including quest offers (for this giver role) and turn-ins (active quests whose
    /// giver is this specific NPC id). Flavor text is the current dialogue line.
    /// </summary>
    private static NpcInteractionEvent BuildInteractionEvent(ref PlayerEntity player, ref TownNpcEntity npc, string flavorText)
    {
        var role = npc.NpcData.Role;
        var offered = new List<int>();
        var turnIns = new List<int>();

        // Quest offers: quests with this giver role that player qualifies for and doesn't already have active/completed.
        var quests = GameData.Instance.Quests.GetForGiverRole(role);
        if (quests.Count > 0 && !player.Quests.AtCapacity)
        {
            for (int i = 0; i < quests.Count; i++)
            {
                var q = quests[i];
                if (player.ClassData.Level < q.MinPlayerLevel) continue;
                if (player.Quests.HasActive(q.NumericId)) continue;
                if (player.Quests.HasCompleted(q.NumericId)) continue;
                bool prereqOk = true;
                for (int p = 0; p < q.PrerequisiteQuestNumericIds.Length; p++)
                {
                    if (!player.Quests.HasCompleted(q.PrerequisiteQuestNumericIds[p])) { prereqOk = false; break; }
                }
                if (!prereqOk) continue;
                offered.Add(q.NumericId);
            }
        }

        // Turn-ins: active quests whose giver is this specific NPC entity.
        if (player.Quests.ActiveQuests != null)
        {
            for (int i = 0; i < player.Quests.ActiveQuests.Count; i++)
            {
                var aq = player.Quests.ActiveQuests[i];
                if (aq.GiverEntityId != npc.Id) continue;
                turnIns.Add(aq.QuestNumericId);
            }
        }

        bool hasShop = GameData.Instance.Shops.GetByRole(role) != null;

        return new NpcInteractionEvent
        {
            PlayerEntityId = player.Id,
            NpcEntityId = npc.Id,
            Npc = npc.Position,
            NpcName = npc.NpcData.Name,
            Text = flavorText,
            NpcRole = (int)role,
            OfferedQuestIds = offered.ToArray(),
            TurnInQuestIds = turnIns.ToArray(),
            HasShop = hasShop,
        };
    }

    private static CombatEvent BuildCombatEvent(
        Position attacker,
        Position target,
        DamageResolution damage,
        bool targetDied,
        bool isRanged = false)
    {
        return new CombatEvent
        {
            Attacker = attacker,
            Target = target,
            Damage = damage.Damage,
            TargetDied = targetDied,
            IsRanged = isRanged,
            DamageType = damage.DamageType,
            WasResisted = damage.WasResisted,
            WasWeakness = damage.WasWeakness,
            StatusEffectType = StatusEffectType.None,
        };
    }
}

public struct CombatEvent
{
    public Position Attacker;
    public Position Target;
    public int Damage;
    public bool TargetDied;
    public bool Blocked;
    public bool IsRanged;
    public DamageType DamageType;
    public bool WasResisted;
    public bool WasWeakness;
    public StatusEffectType StatusEffectType;
}

public struct NpcInteractionEvent
{
    public int PlayerEntityId;
    public int NpcEntityId;
    public Position Npc;
    public string NpcName;
    public string Text;
    public int NpcRole;
    public int[] OfferedQuestIds;
    public int[] TurnInQuestIds;
    public bool HasShop;
}
