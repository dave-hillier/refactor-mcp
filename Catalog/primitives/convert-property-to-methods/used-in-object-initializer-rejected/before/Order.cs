namespace Shop
{
    public class Order
    {
        public int Quantity { get; set; }
    }

    public class Store
    {
        public Order Single() => new Order { Quantity = 1 };
    }
}
