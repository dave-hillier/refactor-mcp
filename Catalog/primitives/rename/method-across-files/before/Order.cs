namespace Shop
{
    public class Order
    {
        public int Total() => 10;

        public int Total(int discount) => Total() - discount;
    }
}
