using System;
using System.Collections.Generic;

namespace Shop;

public sealed class Point : IEquatable<Point>
{
    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; init; }

    public int Y { get; init; }

    public double Length() => Math.Sqrt(X * X + Y * Y);

    public void Deconstruct(out int x, out int y)
    {
        x = X;
        y = Y;
    }

    public bool Equals(Point other)
    {
        return other is not null
            && EqualityComparer<int>.Default.Equals(X, other.X)
            && EqualityComparer<int>.Default.Equals(Y, other.Y);
    }

    public override bool Equals(object obj) => Equals(obj as Point);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public override string ToString() => $"Point {{ X = {X}, Y = {Y} }}";

    public static bool operator ==(Point left, Point right) => left is null ? right is null : left.Equals(right);

    public static bool operator !=(Point left, Point right) => !(left == right);
}
