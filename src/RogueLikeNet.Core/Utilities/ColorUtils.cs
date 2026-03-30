using System.Runtime.CompilerServices;
using Engine.Core;

namespace RogueLikeNet.Core.Utilities;

public static class ColorUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color4 IntToColor4(int packedRgb)
    {
        var cr = (byte)Math.Min(255, packedRgb >> 16 & 0xFF);
        var cg = (byte)Math.Min(255, packedRgb >> 8 & 0xFF);
        var cb = (byte)Math.Min(255, packedRgb & 0xFF);
        return new Color4(cr, cg, cb, 255);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Color4ToInt(Color4 color)
    {
        return (color.R << 16) | (color.G << 8) | color.B;
    }

    // Scale the colors by the given factors (0-100), where 100 means no change
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color4 ScaleColor(Color4 color, int scaleR, int scaleG, int scaleB)
    {
        var cr = (byte)Math.Min(255, color.R * scaleR / 100);
        var cg = (byte)Math.Min(255, color.G * scaleG / 100);
        var cb = (byte)Math.Min(255, color.B * scaleB / 100);
        return new Color4(cr, cg, cb, 255);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color4 ApplyBrightness(Color4 color, float brightness)
    {
        var cr = (byte)Math.Min(255, color.R * brightness);
        var cg = (byte)Math.Min(255, color.G * brightness);
        var cb = (byte)Math.Min(255, color.B * brightness);
        return new Color4(cr, cg, cb, 255);
    }
}
