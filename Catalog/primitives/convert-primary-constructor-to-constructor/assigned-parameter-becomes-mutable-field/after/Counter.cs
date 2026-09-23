namespace Shop;

public class Counter
{
    private int _start;

    public Counter(int start)
    {
        _start = start;
    }

    public int Next() => _start++;
}
