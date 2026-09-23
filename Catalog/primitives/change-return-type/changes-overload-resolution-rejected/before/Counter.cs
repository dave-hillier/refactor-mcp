namespace Shop;

public class Counter
{
    public int Count(int[] items)
    {
        return items.Length;
    }

    public string Print(int value)
    {
        return "int " + value;
    }

    public string Print(long value)
    {
        return "long " + value;
    }

    public string Show(int[] items)
    {
        return Print(Count(items));
    }
}
