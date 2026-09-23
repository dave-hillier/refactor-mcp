namespace Shop;

public static class Stats
{
    public static MinMax Range(int[] values)
    {
        var min = int.MaxValue;
        var max = int.MinValue;
        foreach (var value in values)
        {
            if (value < min) min = value;
            if (value > max) max = value;
        }

        // Empty input gives an inverted range.
        return new MinMax(min, max);
    }
}

public readonly record struct MinMax(int Min, int Max);
