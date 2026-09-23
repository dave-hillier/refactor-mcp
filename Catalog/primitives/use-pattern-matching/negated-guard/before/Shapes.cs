namespace Shop
{
    public class Circle
    {
        public double Radius;
    }

    public class Shapes
    {
        public double Area(object shape)
        {
            /*^*/if (!(shape is Circle))
            {
                return 0;
            }

            var radius = ((Circle)shape).Radius;
            return 3.14 * radius * radius;
        }
    }
}
