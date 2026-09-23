namespace Shop;

public static class Mover
{
    public static Point Right(Point point) => point with { X = point.X + 1 };
}
