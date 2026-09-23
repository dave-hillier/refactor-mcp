namespace Shop
{
    public class Shape
    {
        public virtual double Surface() => 0;
    }

    public class Square : Shape
    {
        public double Side { get; set; }

        public override double Surface() => base.Surface() + Side * Side;
    }
}
