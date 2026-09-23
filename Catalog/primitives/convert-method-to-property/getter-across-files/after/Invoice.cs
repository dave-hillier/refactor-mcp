namespace Shop
{
    public class Invoice
    {
        public string Line(Order order) => "Total: " + order.Total;

        public decimal Tax(Order order) => (order?.Total ?? 0) * 0.2m;
    }
}
