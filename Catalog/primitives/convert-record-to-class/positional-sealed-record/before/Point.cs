using System;

namespace Shop;

public sealed record Point(int X, int Y)
{
    public double Length() => Math.Sqrt(X * X + Y * Y);
}
