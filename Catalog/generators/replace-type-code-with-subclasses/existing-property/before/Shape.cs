namespace Shapes;

public enum ShapeKind
{
    Circle,
    Square
}

public class Shape
{
    private readonly ShapeKind _kind;

    public Shape(ShapeKind kind)
    {
        _kind = kind;
    }

    public ShapeKind Kind => _kind;

    public string Describe() => _kind == ShapeKind.Circle ? "round" : "angular";
}
