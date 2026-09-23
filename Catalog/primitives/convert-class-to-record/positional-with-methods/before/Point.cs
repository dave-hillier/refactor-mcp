using System;

namespace Shop;

/// <summary>A point on the warehouse floor.</summary>
public sealed class Point
{
    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }

    public int Y { get; }

    // Straight-line distance from the loading bay.
    public double Length() => Math.Sqrt(X * X + Y * Y);
}
