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
            /*^*/if (shape is Circle)
            {
                return ((Circle)shape).Radius * 2;
            }

            return 0;
        }
    }
}
