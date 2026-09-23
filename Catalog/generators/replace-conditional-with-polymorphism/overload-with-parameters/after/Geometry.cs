namespace Shapes;

public class Shape
{
    public virtual string Describe(string prefix) => prefix + " shape";
}

public class Circle : Shape
{
    public double Radius { get; set; }

    public override string Describe(string prefix) => prefix + " circle of radius " + Radius;
}

public class Square : Shape
{
    public double Side { get; set; }

    public override string Describe(string prefix) => prefix + " square of side " + Side;
}

public static class Geometry
{
    public static string Describe(Shape shape) => Describe(shape, "a");

    public static string Describe(Shape shape, string prefix) => shape.Describe(prefix);
}
