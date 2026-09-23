using System.Collections.Generic;

namespace Shop;

public static class Lists
{
    public static Bounds<T> Ends<T>(IReadOnlyList<T> items) => new Bounds<T>(items[0], items[items.Count - 1]);

    public static string Outer(IReadOnlyList<string> names) => Ends(names).Head + Ends(names).Tail;
}

public readonly record struct Bounds<T>(T Head, T Tail);
