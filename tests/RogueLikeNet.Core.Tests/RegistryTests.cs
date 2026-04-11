using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;

namespace RogueLikeNet.Core.Tests;

public class RegistryTests
{
    // ── GameData.LogLoadedData ──

    [Fact]
    public void GameData_LogLoadedData_WritesAllCounts()
    {
        var output = new StringWriter();
        GameData.Instance.LogLoadedData(output);
        var text = output.ToString();
        Assert.Contains("[GameData] Loaded", text);
        Assert.Contains("items", text);
        Assert.Contains("recipes", text);
        Assert.Contains("resource nodes", text);
        Assert.Contains("NPCs", text);
        Assert.Contains("biomes", text);
        Assert.Contains("animals", text);
    }

    // ── BaseRegistry: duplicate string ID ──

    [Fact]
    public void BaseRegistry_DuplicateStringId_Throws()
    {
        var registry = new NpcRegistry();
        var defs = new[]
        {
            new NpcDefinition { Id = "dup", Name = "A", Health = 1, Attack = 1, Defense = 0, Speed = 1 },
            new NpcDefinition { Id = "dup", Name = "B", Health = 1, Attack = 1, Defense = 0, Speed = 1 },
        };
        Assert.Throws<InvalidOperationException>(() => registry.Register(defs));
    }

    // ── BaseRegistry: GetNumericId miss ──

    [Fact]
    public void BaseRegistry_GetNumericId_ReturnsZeroForMissing()
    {
        Assert.Equal(0, GameData.Instance.Items.GetNumericId("nonexistent_item_xyz"));
    }

    // ── BaseRegistry: Get returns null for missing ──

    [Fact]
    public void BaseRegistry_Get_ReturnsNullForMissingString()
    {
        Assert.Null(GameData.Instance.Items.Get("nonexistent_item_xyz"));
    }

    [Fact]
    public void BaseRegistry_Get_ReturnsNullForMissingNumericId()
    {
        Assert.Null(GameData.Instance.Items.Get(-999));
    }

    // ── ItemRegistry: Placeable helpers ──

    [Fact]
    public void ItemRegistry_IsPlaceableWalkable_UnknownIdReturnsTrue()
    {
        Assert.True(GameData.Instance.Items.IsPlaceableWalkable(0, 0));
    }

    [Fact]
    public void ItemRegistry_IsPlaceableTransparent_UnknownIdReturnsTrue()
    {
        Assert.True(GameData.Instance.Items.IsPlaceableTransparent(0, 0));
    }

    [Fact]
    public void ItemRegistry_GetPlaceableGlyphId_UnknownIdReturnsZero()
    {
        Assert.Equal(0, GameData.Instance.Items.GetPlaceableGlyphId(0, 0));
    }

    [Fact]
    public void ItemRegistry_GetPlaceableFgColor_UnknownIdReturnsZero()
    {
        Assert.Equal(0, GameData.Instance.Items.GetPlaceableFgColor(0, 0));
    }

    [Fact]
    public void ItemRegistry_IsPlaceableDoor_FalseForNonDoor()
    {
        Assert.False(GameData.Instance.Items.IsPlaceableDoor(0));
    }

    [Fact]
    public void ItemRegistry_IsPlaceableWall_FalseForNonWall()
    {
        Assert.False(GameData.Instance.Items.IsPlaceableWall(0));
    }

    [Fact]
    public void ItemRegistry_IsPlaceableHasState_FalseForUnknown()
    {
        Assert.False(GameData.Instance.Items.IsPlaceableHasState(0));
    }

    [Fact]
    public void ItemRegistry_GetPlaceableCraftingStationType_NullForUnknown()
    {
        Assert.Null(GameData.Instance.Items.GetPlaceableCraftingStationType(0));
    }

    [Fact]
    public void ItemRegistry_PlaceableDoor_Methods()
    {
        var doorDef = GameData.Instance.Items.Get("wooden_door");
        if (doorDef == null) return; // skip if data missing
        int doorId = doorDef.NumericId;

        Assert.True(GameData.Instance.Items.IsPlaceableDoor(doorId));
        Assert.True(GameData.Instance.Items.IsPlaceableDoorClosed(doorId, 0));
        Assert.False(GameData.Instance.Items.IsPlaceableDoorOpen(doorId, 0));
        Assert.True(GameData.Instance.Items.IsPlaceableDoorOpen(doorId, 1));
        Assert.False(GameData.Instance.Items.IsPlaceableDoorClosed(doorId, 1));
    }

    [Fact]
    public void ItemRegistry_PlaceableDoor_WalkableAndTransparent()
    {
        var doorDef = GameData.Instance.Items.Get("wooden_door");
        if (doorDef == null) return;
        int doorId = doorDef.NumericId;

        // Closed door: not walkable, not transparent
        Assert.False(GameData.Instance.Items.IsPlaceableWalkable(doorId, 0));
        Assert.False(GameData.Instance.Items.IsPlaceableTransparent(doorId, 0));

        // Open door: walkable and transparent
        Assert.True(GameData.Instance.Items.IsPlaceableWalkable(doorId, 1));
        Assert.True(GameData.Instance.Items.IsPlaceableTransparent(doorId, 1));
    }

    [Fact]
    public void ItemRegistry_PlaceableDoor_GlyphIdChangesWithState()
    {
        var doorDef = GameData.Instance.Items.Get("wooden_door");
        if (doorDef == null) return;
        int doorId = doorDef.NumericId;

        int closedGlyph = GameData.Instance.Items.GetPlaceableGlyphId(doorId, 0);
        int openGlyph = GameData.Instance.Items.GetPlaceableGlyphId(doorId, 1);
        // open glyph should be the alternate
        Assert.Equal(doorDef.Placeable!.PlacedGlyphId, closedGlyph);
        Assert.Equal(doorDef.Placeable.AlternateGlyphId, openGlyph);
    }

    [Fact]
    public void ItemRegistry_PlaceableFgColor_ReturnsFgColor()
    {
        var doorDef = GameData.Instance.Items.Get("wooden_door");
        if (doorDef == null) return;
        int doorId = doorDef.NumericId;

        Assert.Equal(doorDef.Placeable!.PlacedFgColor, GameData.Instance.Items.GetPlaceableFgColor(doorId, 0));
    }

    [Fact]
    public void ItemRegistry_IsPlaceableHasState_TrueForDoor()
    {
        var doorDef = GameData.Instance.Items.Get("wooden_door");
        if (doorDef == null) return;
        Assert.True(GameData.Instance.Items.IsPlaceableHasState(doorDef.NumericId));
    }

    [Fact]
    public void ItemRegistry_GetAllPlaceables_ReturnsPlaceableItems()
    {
        var placeables = GameData.Instance.Items.GetAllPlaceables();
        Assert.True(placeables.Length > 0);
        Assert.All(placeables, p => Assert.True(p.IsPlaceable));
    }

    [Fact]
    public void ItemRegistry_IsPlaceableWall_TrueForWall()
    {
        var wallDef = GameData.Instance.Items.Get("stone_wall");
        if (wallDef == null) return;
        Assert.True(GameData.Instance.Items.IsPlaceableWall(wallDef.NumericId));
    }

    [Fact]
    public void ItemRegistry_GetPlaceableCraftingStation_ForWorkbench()
    {
        var benchDef = GameData.Instance.Items.Get("crafting_bench");
        if (benchDef == null) return;
        var station = GameData.Instance.Items.GetPlaceableCraftingStationType(benchDef.NumericId);
        Assert.NotNull(station);
        Assert.Equal(CraftingStationType.Workbench, station);
    }

    // ── ItemRegistry: Placeable with no Placeable data (decoration) ──

    [Fact]
    public void ItemRegistry_NonPlaceableItem_WalkableReturnsTrue()
    {
        var swordDef = GameData.Instance.Items.Get("iron_sword");
        if (swordDef == null) return;
        Assert.True(GameData.Instance.Items.IsPlaceableWalkable(swordDef.NumericId, 0));
        Assert.True(GameData.Instance.Items.IsPlaceableTransparent(swordDef.NumericId, 0));
    }

    [Fact]
    public void ItemRegistry_NonPlaceableItem_GlyphIdReturnsFallback()
    {
        // A non-placeable item has Placeable == null, so GetPlaceableGlyphId should return def.GlyphId
        var swordDef = GameData.Instance.Items.Get("iron_sword");
        if (swordDef == null) return;
        Assert.Equal(swordDef.GlyphId, GameData.Instance.Items.GetPlaceableGlyphId(swordDef.NumericId, 0));
    }

    [Fact]
    public void ItemRegistry_NonPlaceableItem_FgColorReturnsFallback()
    {
        var swordDef = GameData.Instance.Items.Get("iron_sword");
        if (swordDef == null) return;
        Assert.Equal(swordDef.FgColor, GameData.Instance.Items.GetPlaceableFgColor(swordDef.NumericId, 0));
    }

    [Fact]
    public void ItemRegistry_Wall_WalkableAndTransparent()
    {
        // Wall: placeable with stateType=none → returns Placeable.Walkable/Transparent directly
        var wallDef = GameData.Instance.Items.Get("stone_wall");
        if (wallDef == null) return;
        int wallId = wallDef.NumericId;
        Assert.False(GameData.Instance.Items.IsPlaceableWalkable(wallId, 0));
        Assert.False(GameData.Instance.Items.IsPlaceableTransparent(wallId, 0));
    }

    [Fact]
    public void ItemRegistry_Wall_GlyphIdReturnsPlacedGlyphId()
    {
        var wallDef = GameData.Instance.Items.Get("stone_wall");
        if (wallDef == null) return;
        int wallId = wallDef.NumericId;
        Assert.Equal(wallDef.Placeable!.PlacedGlyphId, GameData.Instance.Items.GetPlaceableGlyphId(wallId, 0));
    }

    [Fact]
    public void ItemRegistry_Wall_FgColorReturnsFgColor()
    {
        var wallDef = GameData.Instance.Items.Get("stone_wall");
        if (wallDef == null) return;
        int wallId = wallDef.NumericId;
        Assert.Equal(wallDef.Placeable!.PlacedFgColor, GameData.Instance.Items.GetPlaceableFgColor(wallId, 0));
    }

    [Fact]
    public void ItemRegistry_Wall_HasState_ReturnsFalse()
    {
        var wallDef = GameData.Instance.Items.Get("stone_wall");
        if (wallDef == null) return;
        Assert.False(GameData.Instance.Items.IsPlaceableHasState(wallDef.NumericId));
    }

    [Fact]
    public void ItemRegistry_Window_TransparentButNotWalkable()
    {
        var winDef = GameData.Instance.Items.Get("wooden_window");
        if (winDef == null) return;
        int wId = winDef.NumericId;
        Assert.False(GameData.Instance.Items.IsPlaceableWalkable(wId, 0));
        Assert.True(GameData.Instance.Items.IsPlaceableTransparent(wId, 0));
    }

    [Fact]
    public void ItemRegistry_Table_WalkableAndTransparent()
    {
        var tableDef = GameData.Instance.Items.Get("wooden_table");
        if (tableDef == null) return;
        int tId = tableDef.NumericId;
        Assert.True(GameData.Instance.Items.IsPlaceableWalkable(tId, 0));
        Assert.True(GameData.Instance.Items.IsPlaceableTransparent(tId, 0));
    }

    // ── MaterialTiers ──

    [Theory]
    [InlineData(MaterialTier.Wood, 100)]
    [InlineData(MaterialTier.Stone, 130)]
    [InlineData(MaterialTier.Copper, 160)]
    [InlineData(MaterialTier.Iron, 200)]
    [InlineData(MaterialTier.Steel, 250)]
    [InlineData(MaterialTier.Gold, 150)]
    [InlineData(MaterialTier.Mithril, 300)]
    [InlineData(MaterialTier.Adamantite, 400)]
    public void MaterialTiers_GetMultiplier_ReturnsExpected(MaterialTier tier, int expected)
    {
        Assert.Equal(expected, MaterialTiers.GetMultiplier(tier));
    }

    [Fact]
    public void MaterialTiers_GetMultiplier_UnknownTier_Returns100()
    {
        Assert.Equal(100, MaterialTiers.GetMultiplier((MaterialTier)999));
    }

    [Fact]
    public void MaterialTiers_Apply_Iron()
    {
        // 10 * 200 / 100 = 20
        Assert.Equal(20, MaterialTiers.Apply(10, MaterialTier.Iron));
    }

    [Fact]
    public void MaterialTiers_Apply_Wood()
    {
        // 10 * 100 / 100 = 10
        Assert.Equal(10, MaterialTiers.Apply(10, MaterialTier.Wood));
    }

    [Fact]
    public void MaterialTiers_Apply_Adamantite()
    {
        // 10 * 400 / 100 = 40
        Assert.Equal(40, MaterialTiers.Apply(10, MaterialTier.Adamantite));
    }

    // ── GameData: singleton ──

    [Fact]
    public void GameData_Instance_NotNull()
    {
        Assert.NotNull(GameData.Instance);
        Assert.NotNull(GameData.Instance.Items);
        Assert.NotNull(GameData.Instance.Recipes);
        Assert.NotNull(GameData.Instance.ResourceNodes);
        Assert.NotNull(GameData.Instance.Npcs);
        Assert.NotNull(GameData.Instance.Biomes);
        Assert.NotNull(GameData.Instance.Animals);
    }

    [Fact]
    public void GameData_RegistriesHaveData()
    {
        Assert.True(GameData.Instance.Items.Count > 0);
        Assert.True(GameData.Instance.Recipes.Count > 0);
        Assert.True(GameData.Instance.Npcs.Count > 0);
        Assert.True(GameData.Instance.Biomes.Count > 0);
    }

    // ── BaseRegistry: numeric lookup by string ──

    [Fact]
    public void BaseRegistry_GetByNumericId_AfterRegister()
    {
        var item = GameData.Instance.Items.Get("iron_sword");
        if (item == null) return;
        var byNumeric = GameData.Instance.Items.Get(item.NumericId);
        Assert.NotNull(byNumeric);
        Assert.Equal("iron_sword", byNumeric.Id);
    }

    // ── ResourceNodeRegistry ──

    [Fact]
    public void ResourceNodeRegistry_GetAllResourceNodes()
    {
        Assert.True(GameData.Instance.ResourceNodes.Count > 0);
        var allNodes = GameData.Instance.ResourceNodes.All;
        Assert.NotNull(allNodes);
        Assert.True(allNodes.Count() > 0);
    }

    // ── BiomeRegistry ──

    [Fact]
    public void BiomeRegistry_HasBiomes()
    {
        Assert.True(GameData.Instance.Biomes.Count > 0);
    }

    // ── AnimalRegistry ──

    [Fact]
    public void AnimalRegistry_HasAnimals()
    {
        Assert.True(GameData.Instance.Animals.Count > 0);
    }
}
