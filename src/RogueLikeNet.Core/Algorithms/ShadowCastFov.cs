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
    /// Comparison: slopeA > slopeB ⟺ slopeANum * slopeBDen > slopeBNum * slopeADen
    /// </summary>
    private static void CastLight(
        int ox, int oy, int radius, int row,
        int startNum, int startDen,     // start slope = startNum / startDen (initially 1/1)
        int endNum, int endDen,         // end slope = endNum / endDen (initially 0/1)
        int xx, int xy, int yx, int yy,
        Func<int, int, bool> isOpaque,
        Action<int, int> markVisible)
    {
        if (row > radius) return;
        // If start slope <= end slope, the arc is empty
        // start <= end ⟺ startNum * endDen <= endNum * startDen
        if ((long)startNum * endDen <= (long)endNum * startDen) return;

        int sn = startNum, sd = startDen;

        for (int j = row; j <= radius; j++)
        {
            bool blocked = false;
            int newStartNum = sn, newStartDen = sd;

            for (int dx = -j; dx <= 0; dx++)
            {
                int dy = j;
                // Map to actual coordinates via octant transform
                int mapX = ox + dx * xx + dy * xy;
                int mapY = oy + dx * yx + dy * yy;

                // Slope of the left edge of this cell = (dx - 0.5) / (dy + 0.5)
                // Using integers: leftNum = 2*dx - 1, leftDen = 2*dy + 1
                int leftNum = 2 * dx - 1;
                int leftDen = 2 * dy + 1;

                // Slope of the right edge = (dx + 0.5) / (dy - 0.5)
                // rightNum = 2*dx + 1, rightDen = 2*dy - 1
                int rightNum = 2 * dx + 1;
                int rightDen = 2 * dy - 1;
                if (rightDen <= 0) rightDen = 1; // avoid division issues at row 0

                // If right edge < end slope, skip (not in visible arc yet)
                // right < end ⟺ rightNum * endDen < endNum * rightDen
                if ((long)rightNum * endDen < (long)endNum * rightDen)
                    continue;

                // If left edge > start slope, we've gone past the visible arc
                // left > start ⟺ leftNum * startDen > startNum * leftDen  (absolute comparison, note leftNum is negative)
                if (leftDen > 0 && (long)leftNum * sd > (long)sn * leftDen)
                    break;

                // Distance check (squared, to avoid sqrt)
                if (dx * dx + dy * dy <= radius * radius)
                    markVisible(mapX, mapY);

                if (blocked)
                {
                    if (isOpaque(mapX, mapY))
                    {
                        // Still in shadow — update new start slope
                        newStartNum = rightNum;
                        newStartDen = rightDen;
                    }
                    else
                    {
                        blocked = false;
                        sn = newStartNum;
                        sd = newStartDen;
                    }
                }
                else if (isOpaque(mapX, mapY))
                {
                    blocked = true;
                    newStartNum = rightNum;
                    newStartDen = rightDen;

                    // Recurse with narrowed arc
                    CastLight(ox, oy, radius, j + 1,
                        sn, sd, leftNum, leftDen,
                        xx, xy, yx, yy, isOpaque, markVisible);
                }
            }

            if (blocked)
                return;
        }
    }
}
