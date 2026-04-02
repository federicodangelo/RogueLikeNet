namespace RogueLikeNet.Core.Components;

/// <summary>
/// Tag component marking an entity as a player (vs monster/item).
/// </summary>
public struct PlayerTag
{
    public long ConnectionId;
}

/// <summary>
/// Marks an entity for removal at end of tick.
/// </summary>
public struct DeadTag { }

/// <summary>
/// Tag for peaceful town NPCs that wander within their town area.
/// Hitting this NPC triggers a conversation instead of an attack.
/// </summary>
public struct TownNpcTag
{
    /// <summary>Display name of this NPC.</summary>
    public string Name;
    /// <summary>Center X of the town this NPC belongs to (world coords).</summary>
    public int TownCenterX;
    /// <summary>Center Y of the town this NPC belongs to (world coords).</summary>
    public int TownCenterY;
    /// <summary>Maximum wander radius from town center.</summary>
    public int WanderRadius;
    /// <summary>Ticks remaining before the conversation message disappears. 0 = not talking.</summary>
    public int TalkTimer;
    /// <summary>Index into TownNpcDefinitions.Dialogues for current conversation line.</summary>
    public int DialogueIndex;
}
