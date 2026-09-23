using System;

namespace Shapes;

public enum ShapeKind
{
    Circle,
    Square
}

public abstract class Shape
{
    public static Shape Create(ShapeKind kind) => kind switch
    {
        ShapeKind.Circle => new Circle(),
        ShapeKind.Square => new Square(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public abstract ShapeKind Kind { get; }

    public string Describe() => Kind == ShapeKind.Circle ? "round" : "angular";
}

public sealed class Circle : Shape
{
    public override ShapeKind Kind => ShapeKind.Circle;
}

public sealed class Square : Shape
{
    public override ShapeKind Kind => ShapeKind.Square;
}
