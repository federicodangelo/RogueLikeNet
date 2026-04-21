namespace RogueLikeNet.Core.Data;

/// <summary>
/// Registry for quest definitions. Indexed by string/numeric id and also
/// bucketed by giver NPC role for fast "what quests can this NPC offer?" lookups.
/// </summary>
public sealed class QuestRegistry : BaseRegistry<QuestDefinition>
{
    private readonly Dictionary<TownNpcRole, List<QuestDefinition>> _byGiverRole = new();
    private readonly List<QuestDefinition> _emptyList = new();

    private readonly ItemRegistry _items;
    private readonly NpcRegistry _npcs;
    private readonly ResourceNodeRegistry _nodes;
    private readonly BiomeRegistry _biomes;

    public QuestRegistry(ItemRegistry items, NpcRegistry npcs, ResourceNodeRegistry nodes, BiomeRegistry biomes)
    {
        _items = items;
        _npcs = npcs;
        _nodes = nodes;
        _biomes = biomes;
    }

    public IReadOnlyList<QuestDefinition> GetForGiverRole(TownNpcRole role)
    {
        if (_byGiverRole.TryGetValue(role, out var list))
            return list;
        return _emptyList;
    }

    protected override void ExtraRegister(QuestDefinition def)
    {
        // Resolve objective TargetId → TargetNumericId based on objective type
        foreach (var obj in def.Objectives)
        {
            obj.TargetNumericId = obj.Type switch
            {
                QuestObjectiveType.Kill => _npcs.GetNumericId(obj.TargetId),
                QuestObjectiveType.Collect => _items.GetNumericId(obj.TargetId),
                QuestObjectiveType.Deliver => _items.GetNumericId(obj.TargetId),
                QuestObjectiveType.Craft => _items.GetNumericId(obj.TargetId),
                QuestObjectiveType.Harvest => _items.GetNumericId(obj.TargetId),
                QuestObjectiveType.Gather => _nodes.GetNumericId(obj.TargetId),
                QuestObjectiveType.Reach => _biomes.GetNumericId(obj.TargetId),
                _ => 0,
            };
        }

        // Resolve reward item ids
        foreach (var reward in def.Rewards.Items)
            reward.ItemNumericId = _items.GetNumericId(reward.ItemId);

        if (!_byGiverRole.TryGetValue(def.GiverRole, out var list))
        {
            list = new List<QuestDefinition>();
            _byGiverRole[def.GiverRole] = list;
        }
        list.Add(def);
    }

    protected override void PostRegister()
    {
        // Resolve prerequisite quest ids (now all quest numeric ids are known)
        foreach (var def in All)
        {
            if (def.PrerequisiteQuestIds.Length == 0)
            {
                def.PrerequisiteQuestNumericIds = [];
                continue;
            }
            var ids = new int[def.PrerequisiteQuestIds.Length];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = GetNumericId(def.PrerequisiteQuestIds[i]);
            def.PrerequisiteQuestNumericIds = ids;
        }
    }
}
