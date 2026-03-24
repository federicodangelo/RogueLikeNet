namespace RogueLikeNet.Core.Generation;

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
    public const int GlyphDoor = 43;      // +
    public const int GlyphStairsDown = 62; // >
    public const int GlyphStairsUp = 60;   // <
    public const int GlyphWater = 247;    // ≈
    public const int GlyphLava = 247;     // ≈
    public const int GlyphPlayer = 64;    // @
    public const int GlyphGoblin = 103;   // g
    public const int GlyphOrc = 111;      // o
    public const int GlyphSkeleton = 115; // s
    public const int GlyphDragon = 68;    // D
    public const int GlyphPotion = 173;   // ¡
    public const int GlyphSword = 47;     // /
    public const int GlyphShield = 91;    // [
    public const int GlyphGold = 36;      // $
    public const int GlyphTorch = 15;     // ☼

    // Colors (packed 0xRRGGBB)
    public const int ColorWhite = 0xFFFFFF;
    public const int ColorGray = 0x808080;
    public const int ColorDarkGray = 0x404040;
    public const int ColorBlack = 0x000000;
    public const int ColorRed = 0xFF0000;
    public const int ColorGreen = 0x00FF00;
    public const int ColorBlue = 0x0000FF;
    public const int ColorYellow = 0xFFFF00;
    public const int ColorOrange = 0xFF8800;
    public const int ColorBrown = 0x8B4513;
    public const int ColorCyan = 0x00FFFF;
    public const int ColorMagenta = 0xFF00FF;
    public const int ColorDarkBlue = 0x000044;
    public const int ColorDarkRed = 0x440000;
    public const int ColorFloorFg = 0x666666;
    public const int ColorWallFg = 0xAAAAAA;
    public const int ColorDoorFg = 0xCC8844;
    public const int ColorTorchFg = 0xFFCC66;
    public const int ColorLavaFg = 0xFF4400;
    public const int ColorWaterFg = 0x4488FF;
}
