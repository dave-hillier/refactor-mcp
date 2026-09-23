namespace Shop
{
    public class Order
    {
        public int Biggest(int a, int b) => Compare.Larger(a, b) + Compare.Larger<int>(b, a);
    }
}
