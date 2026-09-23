namespace Shapes;

public abstract class Shape
{
    public string Name { get; set; }
}

public sealed class Circle : Shape
{
    public double Radius { get; set; }
}

public sealed class Square : Shape
{
    public double Side { get; set; }
}
