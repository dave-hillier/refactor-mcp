namespace Shop;

public class Range
{
    private readonly int _low;
    private readonly int _high;

    public Range(int low, int high)
    {
        _low = low;
        _high = high;
    }

    public Range(int high) : this(0, high)
    {
    }

    public int Length => _high - _low;
}
