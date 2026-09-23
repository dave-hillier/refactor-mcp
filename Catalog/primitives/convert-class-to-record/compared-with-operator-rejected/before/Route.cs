namespace Shop;

public class Route
{
    public Point Start { get; } = new Point(0, 0);

    public bool EndsAt(Point point) => point == Start;
}
