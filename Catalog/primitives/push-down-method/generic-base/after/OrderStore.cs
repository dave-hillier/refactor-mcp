namespace Shop
{
    public class Order
    {
    }

    public class OrderStore : Store<Order>
    {
        public string First(Order order) => Describe(order);

        public string Describe(Order item) => "Item " + item;
    }
}
