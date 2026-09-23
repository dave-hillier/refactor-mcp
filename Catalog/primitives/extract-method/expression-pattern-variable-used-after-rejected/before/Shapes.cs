namespace Geometry
{
    public class Circle
    {
        public double Radius { get; set; }
    }

    public class Shapes
    {
        public double Radius(object shape)
        {
            if (/*[*/shape is Circle circle/*]*/)
            {
                return circle.Radius;
            }

            return 0;
        }
    }
}
