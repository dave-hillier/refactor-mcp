namespace Shop
{
    public class Report
    {
        public double Sum(Shape shape, Square square) => shape.Area() + square.Area();
    }
}
