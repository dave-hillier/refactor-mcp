namespace Shop;

public static class Stats
{
    public static (int, int) Range(int[] values) => (values[0], values[values.Length - 1]);
}
