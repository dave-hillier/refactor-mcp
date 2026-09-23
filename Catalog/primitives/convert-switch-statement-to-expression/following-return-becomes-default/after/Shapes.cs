namespace Shop
{
    public class Circle
    {
        public double Radius;
    }

    public class Shapes
    {
        public string Describe(object shape)
        {
            return shape switch
            {
                null => "nothing",
                Circle circle when circle.Radius > 10 => "big circle",
                Circle => "circle",
                _ => "unknown",
            };
        }
    }
}
