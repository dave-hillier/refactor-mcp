namespace Shop
{
    public class Order
    {
        public decimal Price(int quantity, decimal itemPrice)
        {
            if (quantity > 10)
            {
                decimal total = quantity * itemPrice * 0.9m;
                return total;
            }
            else
            {
                decimal total = quantity * itemPrice;
                return total;
            }
        }
    }
}
