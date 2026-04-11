namespace RogueLikeNet.Core.Definitions;

/// <summary>
/// Common glyph IDs (CP437 charset indices) and packed RGB colors for map generation.
/// These are game-semantic constants — the client maps them to visuals.
/// </summary>
public static class TileDefinitions
{
    // Glyph IDs (CP437)
    public const int GlyphVoid = 0;       // null character
    public const int GlyphFloor = 250;    // middle dot ·
    public const int GlyphWall = 219;     // full block █
    public const int GlyphDoor = 0;      // " " (open door, fallback)
    public const int GlyphDoorVertical = 179;   // | (door between N/S walls)
    public const int GlyphDoorHorizontal = 196;  // ─ (door between E/W walls)
    public const int GlyphStairsDown = 62; // >
    public const int GlyphStairsUp = 60;   // <
    public const int GlyphPlayer = 64;    // @
    public const int GlyphTorch = 15;     // ☼

    // Dropped resource item glyphs (distinct from nodes)
    public const int GlyphLog = 61;        // = (log)
    public const int GlyphOreNugget = 7;   // • (nugget)

    // Generic glyph for dropped placeable items on the ground
    public const int GlyphDroppedPlaceable = 254; // ■

    // Colors (packed 0xRRGGBB)
    public const int ColorWhite = 0xFFFFFF;
    public const int ColorBlack = 0x000000;
    public const int ColorFloorFg = 0x666666;
    public const int ColorWallFg = 0xAAAAAA;
    public const int ColorTorchFg = 0xFFCC66;

    // NPC glyphs
    public const int GlyphTownNpc = 2;     // ☻ (smiling face)
    public const int ColorTownNpcFg = 0x44AAFF;

    // Crop growth stage glyphs (CP437)
    public const int GlyphCropStage0 = 250; // · (seedling, same as floor dot)
    public const int GlyphCropStage1 = 44;  // , (small sprout)
    public const int GlyphCropStage2 = 244; // ⌠ (growing plant)
    public const int GlyphCropStage3 = 157; // Ø (mature crop, ready to harvest)

    public const int ColorCropSeedling = 0x665533;
    public const int ColorCropGrowing = 0x44AA22;
    public const int ColorCropMature = 0xDDAA00;
    public const int ColorTilledSoil = 0x553311;
}
