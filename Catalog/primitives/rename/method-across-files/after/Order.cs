namespace Shop
{
    public class Order
    {
        public int Total() => 10;

        public int TotalWithDiscount(int discount) => Total() - discount;
    }
}
