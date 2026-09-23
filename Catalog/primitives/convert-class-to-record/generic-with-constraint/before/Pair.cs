using System;

namespace Shop;

public sealed class Pair<T> where T : IComparable<T>
{
    public Pair(T first, T second)
    {
        First = first;
        Second = second;
    }

    public T First { get; }

    public T Second { get; }

    public T Larger => First.CompareTo(Second) >= 0 ? First : Second;
}
