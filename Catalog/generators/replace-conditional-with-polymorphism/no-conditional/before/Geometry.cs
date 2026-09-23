namespace Shapes;

public static class Geometry
{
    public static string Label(Shape shape)
    {
        var name = shape.Name;
        return name.ToUpperInvariant();
    }
}
