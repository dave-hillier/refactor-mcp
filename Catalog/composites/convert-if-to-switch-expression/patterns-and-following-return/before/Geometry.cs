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
            /*^*/if (shape is Circle c && c.Radius > 0)
            {
                return Math.PI * c.Radius * c.Radius;
            }
            else if (shape is Square s)
            {
                return s.Side * s.Side;
            }
            else if (shape is null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            return 0;
        }
    }
}
