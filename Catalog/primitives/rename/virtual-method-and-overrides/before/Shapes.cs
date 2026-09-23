namespace Shop
{
    public class Shape
    {
        public virtual double Area() => 0;
    }

    public class Square : Shape
    {
        public double Side { get; set; }

        public override double Area() => base.Area() + Side * Side;
    }
}
