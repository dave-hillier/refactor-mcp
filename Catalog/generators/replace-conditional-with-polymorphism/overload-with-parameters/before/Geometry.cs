namespace Shapes;

public class Shape
{
}

public class Circle : Shape
{
    public double Radius { get; set; }
}

public class Square : Shape
{
    public double Side { get; set; }
}

public static class Geometry
{
    public static string Describe(Shape shape) => Describe(shape, "a");

    public static string Describe(Shape shape, string prefix) => shape switch
    {
        Circle c => prefix + " circle of radius " + c.Radius,
        Square s => prefix + " square of side " + s.Side,
        _ => prefix + " shape",
    };
}
