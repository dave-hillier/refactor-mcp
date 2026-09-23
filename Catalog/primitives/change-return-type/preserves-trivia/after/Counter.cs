using System;

namespace Shop;

public static class Counter
{
    /// <summary>Counts the items.</summary>
    [Obsolete("Use Length")]
    public static /* small */ long Count(int[] items)
    {
        return items.Length;
    }
}
