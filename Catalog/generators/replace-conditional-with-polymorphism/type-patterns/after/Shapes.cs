using System;

namespace Shapes;

public abstract class Shape
{
    public string Name { get; set; }

    public abstract double Area();
}

public sealed class Circle : Shape
{
    public double Radius { get; set; }

    public override double Area() => Math.PI * Radius * Radius;
}

public sealed class Square : Shape
{
    public double Side { get; set; }

    public override double Area() => Side * Side;
}
