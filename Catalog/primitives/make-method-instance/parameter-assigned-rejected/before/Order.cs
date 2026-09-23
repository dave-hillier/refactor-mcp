namespace Shop
{
    public class Order
    {
        public int Sequence { get; set; }

        public static Order Latest(Order order, Order other)
        {
            if (other.Sequence > order.Sequence)
                order = other;
            return order;
        }
    }
}
