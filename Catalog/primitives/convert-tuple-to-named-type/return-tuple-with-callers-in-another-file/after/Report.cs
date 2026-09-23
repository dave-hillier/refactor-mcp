namespace Shop;

public static class Report
{
    public static string Describe(int[] values)
    {
        var range = Stats.Range(values);
        return range.Min + ".." + range.Max;
    }

    public static int Width(int[] values)
    {
        var (low, high) = Stats.Range(values);
        return high - low;
    }

    public static int Top(int[] values) => Stats.Range(values).Max;
}
