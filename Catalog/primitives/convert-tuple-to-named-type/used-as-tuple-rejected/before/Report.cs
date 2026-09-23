namespace Shop;

public static class Report
{
    public static int Width(int[] values)
    {
        (int min, int max) range = Stats.Range(values);
        return range.max - range.min;
    }
}
