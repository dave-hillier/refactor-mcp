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
            /*^*/if (shape is Circle circle)
            {
                return 3.14 * circle.Radius * circle.Radius;
            }
            else
            {
                return 0;
            }
        }
    }
}
