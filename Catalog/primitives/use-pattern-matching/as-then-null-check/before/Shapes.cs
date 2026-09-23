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
            var circle = shape as Circle;
            /*^*/if (circle != null)
            {
                return circle.Radius;
            }

            return 0;
        }
    }
}
