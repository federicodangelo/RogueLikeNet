namespace RogueLikeNet.Core.Definitions;

/// <summary>
/// Glyph IDs and colors for entities, items, and UI elements that are not
/// part of the tile registry (player, NPCs, dropped items, torches, etc.).
/// </summary>
public static class RenderConstants
{
    // Entity glyphs (CP437)
    public const int GlyphPlayer = 64;
    public const int GlyphTownNpc = 2;

    // Door glyphs
    public const int GlyphDoor = 0;
    public const int GlyphDoorVertical = 179;
    public const int GlyphDoorHorizontal = 196;

    // Dropped item glyphs
    public const int GlyphLog = 61;
    public const int GlyphOreNugget = 7;
    public const int GlyphDroppedPlaceable = 254;

    // Colors (packed 0xRRGGBB)
    public const int ColorWhite = 0xFFFFFF;
    public const int ColorBlack = 0x000000;
    public const int ColorTorchFg = 0xFFCC66;
    public const int ColorTownNpcFg = 0x44AAFF;

    // Town NPC role colors
    public const int ColorNpcVillager = 0x44AAFF;   // Light blue
    public const int ColorNpcMerchant = 0xFFD700;    // Gold
    public const int ColorNpcBlacksmith = 0xFF6633;  // Orange-red
    public const int ColorNpcFarmer = 0x66CC44;      // Green
    public const int ColorNpcAlchemist = 0xCC44FF;   // Purple
    public const int ColorNpcGuard = 0xCCCCCC;       // Silver
    public const int ColorNpcInnkeeper = 0xFFAA55;   // Warm amber

    // Crop growth stage glyphs (CP437)
    public const int GlyphCropStage0 = 250;
    public const int GlyphCropStage1 = 44;
    public const int GlyphCropStage2 = 244;
    public const int GlyphCropStage3 = 157;

    public const int ColorCropSeedling = 0x665533;
    public const int ColorCropGrowing = 0x44AA22;
    public const int ColorCropMature = 0xDDAA00;
    public const int ColorTilledSoil = 0x553311;
}
