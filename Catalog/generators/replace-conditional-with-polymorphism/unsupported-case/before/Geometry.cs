namespace Shapes;

public static class Geometry
{
    public static bool IsLarge(Shape shape)
    {
        switch (shape)
        {
            case Circle c when c.Radius > 10:
                return true;
            case Square s:
                return s.Side > 20;
            default:
                return false;
        }
    }
}
