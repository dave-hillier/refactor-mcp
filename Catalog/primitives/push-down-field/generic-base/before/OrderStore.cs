namespace Shop
{
    public class Order
    {
    }

    public class OrderStore : Store<Order>
    {
        public override int Count() => Cache.Count;
    }
}
