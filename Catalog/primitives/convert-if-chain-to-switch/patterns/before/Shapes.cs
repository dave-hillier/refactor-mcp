namespace Shop
{
    public class Circle
    {
        public double Radius;
    }

    public class Square
    {
        public double Side;
    }

    public class Shapes
    {
        public string Describe(object shape)
        {
            /*^*/if (shape is null)
            {
                return "nothing";
            }
            else if (shape is Circle circle && circle.Radius > 10)
            {
                return "big circle";
            }
            else if (shape is Circle)
            {
                return "circle";
            }
            else if (shape is Square square)
            {
                return "square of " + square.Side;
            }

            return "unknown";
        }
    }
}
