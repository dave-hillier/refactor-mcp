namespace Shop
{
    public class Order
    {
        public int Quantity { get; set; } = 1;

        public bool IsBulk() => Quantity > 10;
    }
}
