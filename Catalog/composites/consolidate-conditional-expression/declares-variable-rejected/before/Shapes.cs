namespace Drawing
{
    public class Circle
    {
        public double Radius { get; set; }
    }

    public class Shapes
    {
        public double Size(object shape, bool empty)
        {
            /*^*/if (shape is Circle c)
            {
                return 0;
            }

            if (empty)
            {
                return 0;
            }

            return 1;
        }
    }
}
