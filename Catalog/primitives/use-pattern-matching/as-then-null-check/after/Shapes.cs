namespace Shop
{
    public class Circle
    {
        public double Radius;
    }

    public class Shapes
    {
        public double Radius(object shape)
        {
            // Only circles have a radius.
            if (shape is Circle circle)
            {
                return circle.Radius;
            }

            return 0;
        }
    }
}
