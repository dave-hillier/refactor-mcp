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
            if (shape is not Circle circle)
            {
                return 0;
            }
            else
            {
                return 3.14 * circle.Radius * circle.Radius;
            }
        }
    }
}
