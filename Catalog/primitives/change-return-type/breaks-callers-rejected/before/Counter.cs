namespace Shop;

public class Counter
{
    public int Count(int[] items)
    {
        return items.Length;
    }

    public int Total(int[] items)
    {
        int total = Count(items);
        return total;
    }
}
