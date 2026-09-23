namespace Shop
{
    public class Order
    {
        public decimal Total(decimal subtotal)
        {
            return Discounted(subtotal);
        }

        public static decimal Discounted(decimal amount) => amount * 0.9m;
    }
}
