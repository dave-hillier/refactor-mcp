namespace Shop
{
    public class Shape
    {
        public virtual double Area() => 0;
    }

    public class Square : Shape
    {
        public override double Area() => 4;
    }
}
