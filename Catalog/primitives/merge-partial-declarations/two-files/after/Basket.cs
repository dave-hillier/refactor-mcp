namespace Shop;

public class Basket
{
    public int Count { get; private set; }

    public void Add()
    {
        Count++;
    }

    public void Clear()
    {
        Count = 0;
    }
}
