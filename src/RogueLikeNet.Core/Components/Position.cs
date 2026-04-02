using System.Runtime.CompilerServices;
namespace RogueLikeNet.Core.Components;

public struct Position
{
    public const int DefaultZ = 127;

    public int X;
    public int Y;
    public int Z;

    public Position(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public override string ToString() => $"({X}, {Y}, {Z})";

    public static bool operator ==(Position a, Position b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    public static bool operator !=(Position a, Position b) => !(a == b);
    public override bool Equals(object? obj) => obj is Position p && this == p;
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ManhattanDistance(Position a, Position b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ManhattanDistance2D(int x1, int y1, int x2, int y2) => Math.Abs(x1 - x2) + Math.Abs(y1 - y2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ChebyshevDistance(Position a, Position b) => Math.Max(Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)), Math.Abs(a.Z - b.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ChebyshevDistance(int x1, int y1, int x2, int y2) => Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));

    /// <summary>
    /// Packs X (24-bit signed), Y (24-bit signed), Z (8-bit unsigned 0-255) into a 64-bit long.
    /// Layout: [bits 55-32: X] [bits 31-8: Y] [bits 7-0: Z]. Top 8 bits unused.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long PackCoord(int x, int y, int z)
    {
        return ((long)(x & 0xFFFFFF) << 32) | ((long)(y & 0xFFFFFF) << 8) | (long)(z & 0xFF);
    }

    /// <summary>
    /// Unpacks a 64-bit long into X (24-bit sign-extended), Y (24-bit sign-extended), Z (8-bit unsigned).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int X, int Y,
    int Z) UnpackCoord(long packed)
    {
        return (
                (int)(((packed >> 32) & 0xFFFFFF) << 8) >> 8,
                (int)(((packed >> 8) & 0xFFFFFF) << 8) >> 8,
                (int)(packed & 0xFF)
        );
    }
}
