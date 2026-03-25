namespace RogueLikeNet.Core.Algorithms;

/// <summary>
/// Recursive shadow casting FOV algorithm.
/// Uses integer-only slope comparison (multiplied cross-products instead of float division).
/// 8-octant symmetry — compute one octant, mirror for all 8.
/// </summary>
public static class ShadowCastFov
{
    // Octant multipliers for transforming coordinates
    //                      NNE ENE ESE SSE SSW WSW WNW NNW
    private static readonly int[] OctXx = [1, 0, 0, -1, -1, 0, 0, 1];
    private static readonly int[] OctXy = [0, 1, -1, 0, 0, -1, 1, 0];
    private static readonly int[] OctYx = [0, 1, 1, 0, 0, -1, -1, 0];
    private static readonly int[] OctYy = [1, 0, 0, 1, -1, 0, 0, -1];

    /// <summary>
    /// Computes FOV from (originX, originY) with given radius.
    /// Calls <paramref name="markVisible"/> for each visible tile.
    /// <paramref name="isOpaque"/> returns true if a tile blocks sight.
    /// </summary>
    public static void Compute(
        int originX, int originY, int radius,
        Func<int, int, bool> isOpaque,
        Action<int, int> markVisible)
    {
        markVisible(originX, originY);

        for (int octant = 0; octant < 8; octant++)
        {
            CastLight(originX, originY, radius, 1,
                1, 1, // start slope = 1/1
                0, 1, // end slope = 0/1
                OctXx[octant], OctXy[octant], OctYx[octant], OctYy[octant],
                isOpaque, markVisible);
        }
    }

    /// <summary>
    /// Slopes are represented as pairs (numerator, denominator) to avoid floats.
    /// startSlopeNum/startSlopeDen and endSlopeNum/endSlopeDen define the visible arc.
    /// Slopes are represented as pairs (numerator, denominator) to avoid floats.
    /// startSlope (initially 1/1) is the high boundary (diagonal side).
    /// endSlope (initially 0/1) is the low boundary (cardinal side).
    /// Scan order: HIGH-to-LOW (from diagonal toward cardinal).
    /// </summary>
    private static void CastLight(
        int ox, int oy, int radius, int row,
        int startNum, int startDen,
        int endNum, int endDen,
        int xx, int xy, int yx, int yy,
        Func<int, int, bool> isOpaque,
        Action<int, int> markVisible)
    {
        if (row > radius) return;
        // If start slope <= end slope, the arc is empty
        if ((long)startNum * endDen <= (long)endNum * startDen) return;

        int sn = startNum, sd = startDen;

        for (int j = row; j <= radius; j++)
        {
            bool blocked = false;
            int newStartNum = sn, newStartDen = sd;

            // Scan from diagonal (col=j) toward cardinal (col=0) — HIGH slope to LOW slope.
            // dx is negative: dx=-col, so the octant transform produces correct world coords.
            for (int col = j; col >= 0; col--)
            {
                int dx = -col;
                int dy = j;
                int mapX = ox + dx * xx + dy * xy;
                int mapY = oy + dx * yx + dy * yy;

                // Low-slope edge (cardinal side): (col - 0.5) / (row + 0.5)
                int loNum = 2 * col - 1;
                int loDen = 2 * dy + 1;

                // High-slope edge (diagonal side): (col + 0.5) / (row - 0.5)
                int hiNum = 2 * col + 1;
                int hiDen = 2 * dy - 1;
                if (hiDen <= 0) hiDen = 1;

                // If low edge > start slope, this cell is above our arc — skip
                if (loDen > 0 && (long)loNum * sd > (long)sn * loDen)
                    continue;

                // If high edge < end slope, this cell is below our arc — done with this row
                if ((long)hiNum * endDen < (long)endNum * hiDen)
                    break;

                // Distance check (Euclidean)
                if (col * col + dy * dy <= radius * radius)
                    markVisible(mapX, mapY);

                if (blocked)
                {
                    if (isOpaque(mapX, mapY))
                    {
                        // Still in shadow — track the cardinal-side edge
                        newStartNum = loNum;
                        newStartDen = loDen;
                    }
                    else
                    {
                        // Leaving shadow — narrow start to the shadow's cardinal edge
                        blocked = false;
                        sn = newStartNum;
                        sd = newStartDen;
                    }
                }
                else if (isOpaque(mapX, mapY))
                {
                    blocked = true;
                    newStartNum = loNum;
                    newStartDen = loDen;

                    // Recurse for the arc above this wall (from current start to wall's diagonal edge)
                    CastLight(ox, oy, radius, j + 1,
                        sn, sd, hiNum, hiDen,
                        xx, xy, yx, yy, isOpaque, markVisible);
                }
            }

            if (blocked)
                return;
        }
    }
}
