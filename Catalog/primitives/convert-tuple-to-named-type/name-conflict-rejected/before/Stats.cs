namespace Shop;

public class Range
{
}

public static class Stats
{
    public static (int min, int max) Range(int[] values) => (values[0], values[values.Length - 1]);
}
