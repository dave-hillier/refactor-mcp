using System;

namespace Shop;

public sealed record Pair<T>(T First, T Second) where T : IComparable<T>
{
    public T Larger => First.CompareTo(Second) >= 0 ? First : Second;
}
