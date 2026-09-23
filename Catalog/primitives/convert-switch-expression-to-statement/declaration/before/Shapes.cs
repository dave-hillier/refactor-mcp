using System;

namespace Shop
{
    public class Circle
    {
        public double Radius;
    }

    public class Shapes
    {
        public void Print(object shape)
        {
            var name = shape /*^*/switch
            {
                Circle circle when circle.Radius > 10 => "big circle",
                Circle => "circle",
                _ => "unknown",
            };
            Console.WriteLine(name);
        }
    }
}
