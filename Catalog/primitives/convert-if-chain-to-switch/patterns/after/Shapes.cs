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
            switch (shape)
            {
                case null:
                    return "nothing";
                case Circle circle when circle.Radius > 10:
                    return "big circle";
                case Circle:
                    return "circle";
                case Square square:
                    return "square of " + square.Side;
            }

            return "unknown";
        }
    }
}
