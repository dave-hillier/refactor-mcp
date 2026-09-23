using System.Collections.Generic;

namespace Shop;

public static class Lists
{
    public static (T head, T tail) Ends<T>(IReadOnlyList<T> items) => (items[0], items[items.Count - 1]);

    public static string Outer(IReadOnlyList<string> names) => Ends(names).head + Ends(names).tail;
}
