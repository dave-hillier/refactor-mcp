namespace Shop;

public static class Stats
{
    public static (int min, int max) Range(int[] values)
    {
        var min = int.MaxValue;
        var max = int.MinValue;
        foreach (var value in values)
        {
            if (value < min) min = value;
            if (value > max) max = value;
        }

        // Empty input gives an inverted range.
        return (min, max);
    }
}
