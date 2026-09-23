namespace Shop
{
    public class Circle
    {
        public double Radius;
    }

    public class Shapes
    {
        public double Diameter(object shape)
        {
            if (shape is Circle c)
            {
                return c.Radius * 2;
            }

            return 0;
        }
    }
}
