using System;

namespace Shapes;

public class Shape
{
}

public sealed class Circle : Shape
{
    public double Radius { get; set; }
}

public sealed class Square : Shape
{
    public double Side { get; set; }
}

public static class Geometry
{
    public static double Area(Shape shape) => shape switch
    {
        Circle c => Math.PI * c.Radius * c.Radius,
        Square s => s.Side * s.Side,
        _ => throw new ArgumentException("Unknown shape"),
    };
}
