using System;

namespace Shop;

/// <summary>A point on the warehouse floor.</summary>
public sealed record Point(int X, int Y)
{
    // Straight-line distance from the loading bay.
    public double Length() => Math.Sqrt(X * X + Y * Y);
}
