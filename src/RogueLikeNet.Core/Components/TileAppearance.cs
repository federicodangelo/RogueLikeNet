using Arch.AOT.SourceGenerator;

namespace RogueLikeNet.Core.Components;

/// <summary>
/// Game-semantic tile appearance data. All int values.
/// GlyphId maps to CP437 charset index. Colors are packed 0xRRGGBB.
/// </summary>
[Component]
public struct TileAppearance
{
    public int GlyphId;
    public int FgColor;
    public int BgColor;

    public TileAppearance(int glyphId, int fgColor, int bgColor = 0x000000)
    {
        GlyphId = glyphId;
        FgColor = fgColor;
        BgColor = bgColor;
    }
}
