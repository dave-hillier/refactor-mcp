namespace Shop
{
    public class Order
    {
        public decimal Total { get; set; }

        public decimal Difference(Order order)
        {
            decimal difference = Total - order.Total;
            return difference;
        }
    }
}
