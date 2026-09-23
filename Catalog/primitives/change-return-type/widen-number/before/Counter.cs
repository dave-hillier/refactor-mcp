namespace Shop;

public class Counter
{
    public int Count(int[] items)
    {
        return items.Length;
    }

    public long Total(int[] items)
    {
        long total = Count(items);
        return total;
    }
}
