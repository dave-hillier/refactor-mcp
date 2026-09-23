using System;

namespace Shapes;

public static class Geometry
{
    public static double Area(Shape shape)
    {
        switch (shape)
        {
            case Circle c:
                return Math.PI * c.Radius * c.Radius;
            case Square s:
                return s.Side * s.Side;
            default:
                throw new ArgumentException("Unknown shape", nameof(shape));
        }
    }
}
