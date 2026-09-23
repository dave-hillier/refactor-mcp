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
            string name;
            switch (shape)
            {
                case Circle circle when circle.Radius > 10:
                    name = "big circle";
                    break;
                case Circle:
                    name = "circle";
                    break;
                default:
                    name = "unknown";
                    break;
            }
            Console.WriteLine(name);
        }
    }
}
