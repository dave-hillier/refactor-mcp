namespace Shapes;

public class Geometry
{
    private readonly double _scale = 2;

    public double Area(Shape shape)
    {
        switch (shape)
        {
            case Circle c:
                return 3 * c.Radius * c.Radius;
            case Square s:
                return s.Side * s.Side * _scale;
            default:
                return 0;
        }
    }
}
