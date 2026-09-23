using System;

namespace Shop;

public readonly struct Range<T> where T : IComparable<T>
{
    public Range(T low, T high)
    {
        Low = low;
        High = high;
    }

    public T Low { get; }

    public T High { get; }
}
