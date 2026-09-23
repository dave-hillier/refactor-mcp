namespace Geometry;

public record Point(double X, double Y);

public class Circle
{
    public Point Centre { get; init; }

    public double Radius { get; init; }
}
