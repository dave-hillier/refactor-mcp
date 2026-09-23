namespace Shop;

public record Point
{
    public Point(int x)
    {
        X = x;
    }

    public int X { get; }
}
