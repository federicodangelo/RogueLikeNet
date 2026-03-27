using System.Runtime.CompilerServices;
using Arch.AOT.SourceGenerator;

namespace RogueLikeNet.Core.Components;

[Component]
public struct Position
{
    public int X;
    public int Y;

    public Position(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override string ToString() => $"({X}, {Y})";

    public static bool operator ==(Position a, Position b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Position a, Position b) => !(a == b);
    public override bool Equals(object? obj) => obj is Position p && this == p;
    public override int GetHashCode() => HashCode.Combine(X, Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ManhattanDistance(Position a, Position b)
            => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ManhattanDistance(int x1, int y1, int x2, int y2) => Math.Abs(x1 - x2) + Math.Abs(y1 - y2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ChebyshevDistance(Position a, Position b)
            => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ChebyshevDistance(int x1, int y1, int x2, int y2)
            => Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long PackCoord(int x, int y) => ((long)x << 32) | (uint)y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int X, int Y) UnpackCoord(long packed) => ((int)(packed >> 32), (int)(packed & 0xFFFFFFFF));

}
