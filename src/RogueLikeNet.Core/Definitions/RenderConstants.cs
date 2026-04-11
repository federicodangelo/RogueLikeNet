namespace RogueLikeNet.Core.Definitions;

/// <summary>
/// Glyph IDs and colors for entities, items, and UI elements that are not
/// part of the tile registry (player, NPCs, dropped items, torches, etc.).
/// </summary>
public static class RenderConstants
{
    // Entity glyphs (CP437)
    public const int GlyphPlayer = 64;
    public const int GlyphTorch = 15;
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
