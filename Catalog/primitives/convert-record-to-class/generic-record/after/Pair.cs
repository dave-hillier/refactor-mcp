using System;
using System.Collections.Generic;

namespace Shop;

public sealed class Pair<T> : IEquatable<Pair<T>> where T : IComparable<T>
{
    public Pair(T first, T second)
    {
        First = first;
        Second = second;
    }

    public T First { get; init; }

    public T Second { get; init; }

    public T Larger => First.CompareTo(Second) >= 0 ? First : Second;

    public void Deconstruct(out T first, out T second)
    {
        first = First;
        second = Second;
    }

    public bool Equals(Pair<T> other)
    {
        return other is not null
            && EqualityComparer<T>.Default.Equals(First, other.First)
            && EqualityComparer<T>.Default.Equals(Second, other.Second);
    }

    public override bool Equals(object obj) => Equals(obj as Pair<T>);

    public override int GetHashCode() => HashCode.Combine(First, Second);

    public override string ToString() => $"Pair {{ First = {First}, Second = {Second}, Larger = {Larger} }}";

    public static bool operator ==(Pair<T> left, Pair<T> right) => left is null ? right is null : left.Equals(right);

    public static bool operator !=(Pair<T> left, Pair<T> right) => !(left == right);
}
