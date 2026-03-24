namespace RogueLikeNet.Core.Algorithms;

/// <summary>
/// Bresenham's line algorithm. Pure integer arithmetic.
/// </summary>
public static class Bresenham
{
    public static void Line(int x0, int y0, int x1, int y1, Action<int, int> plot)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            plot(x0, y0);
            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    /// <summary>
    /// Returns true if there's a clear line of sight from (x0,y0) to (x1,y1).
    /// <paramref name="isBlocking"/> returns true for tiles that block sight.
    /// The start and end tiles are excluded from the blocking check.
    /// </summary>
    public static bool HasLineOfSight(int x0, int y0, int x1, int y1, Func<int, int, bool> isBlocking)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        bool first = true;

        while (true)
        {
            if (!first && !(x0 == x1 && y0 == y1))
            {
                if (isBlocking(x0, y0)) return false;
            }
            first = false;

            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
        return true;
    }
}
