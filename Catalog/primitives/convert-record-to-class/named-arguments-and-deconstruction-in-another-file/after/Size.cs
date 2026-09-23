using System;
using System.Collections.Generic;

namespace Shop;

public sealed class Size : IEquatable<Size>
{
    public Size(decimal width, decimal height)
    {
        Width = width;
        Height = height;
    }

    public decimal Width { get; init; }

    public decimal Height { get; init; }

    public void Deconstruct(out decimal width, out decimal height)
    {
        width = Width;
        height = Height;
    }

    public bool Equals(Size other)
    {
        return other is not null
            && EqualityComparer<decimal>.Default.Equals(Width, other.Width)
            && EqualityComparer<decimal>.Default.Equals(Height, other.Height);
    }

    public override bool Equals(object obj) => Equals(obj as Size);

    public override int GetHashCode() => HashCode.Combine(Width, Height);

    public override string ToString() => $"Size {{ Width = {Width}, Height = {Height} }}";

    public static bool operator ==(Size left, Size right) => left is null ? right is null : left.Equals(right);

    public static bool operator !=(Size left, Size right) => !(left == right);
}
