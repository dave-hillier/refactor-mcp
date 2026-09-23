using System;

namespace Drawing
{
    public abstract class Shape
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

    public class Geometry
    {
        public double Area(Shape shape)
        {
            return shape switch
            {
                Circle c when c.Radius > 0 => Math.PI * c.Radius * c.Radius,
                Square s => s.Side * s.Side,
                null => throw new ArgumentNullException(nameof(shape)),
                _ => 0,
            };
        }
    }
}
