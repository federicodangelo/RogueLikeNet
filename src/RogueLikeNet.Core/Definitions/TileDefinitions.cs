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

    // Decoration glyphs (CP437)
    public const int GlyphPillar = 9;     // ○
    public const int GlyphMushroom = 6;   // ♠
    public const int GlyphCrystal = 4;    // ♦
    public const int GlyphGrass = 44;     // ,
    public const int GlyphMoss = 39;      // '
    public const int GlyphRubble = 177;   // ▒
    public const int GlyphBones = 229;    // σ (bone-like)
    public const int GlyphCoffin = 254;   // ■
    public const int GlyphRune = 42;      // *
    public const int GlyphWeb = 37;       // %
    public const int GlyphCrack = 126;    // ~
    public const int GlyphEmber = 7;      // •
    public const int GlyphIcicle = 124;   // |
    public const int GlyphVines = 244;    // ⌠
    public const int GlyphBarrel = 232;   // Φ
    public const int GlyphStatue = 234;   // Ω

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

    // Decoration colors
    public const int ColorMossFg = 0x4A7A3A;
    public const int ColorMushroomFg = 0xCC66AA;
    public const int ColorCrystalFg = 0x88CCFF;
    public const int ColorBonesFg = 0xCCBB99;
    public const int ColorRuneFg = 0xCC88FF;
    public const int ColorIceFg = 0xAADDFF;
    public const int ColorEmberFg = 0xFF6622;
    public const int ColorRubbleFg = 0x888877;
    public const int ColorVinesFg = 0x338833;
    public const int ColorWebFg = 0xBBBBCC;
    public const int ColorGrassFg = 0x558844;
    public const int ColorCrackFg = 0x664422;
    public const int ColorBarrelFg = 0x996633;
    public const int ColorStatueFg = 0x999999;
    public const int ColorLavaBg = 0x331100;
    public const int ColorWaterBg = 0x001133;
    public const int ColorIceBg = 0x0A1122;
}
