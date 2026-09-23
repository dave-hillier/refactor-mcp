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
            /*^*/if (shape is Circle)
            {
                var circle = (Circle)shape;
                return circle.Radius;
            }

            return 0;
        }
    }
}
