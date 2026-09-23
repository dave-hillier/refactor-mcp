namespace Shop
{
    public class Order
    {
        public int Quantity;

        public int Weight() => Quantity * 10;
    }
}
