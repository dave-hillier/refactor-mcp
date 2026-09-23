namespace Shop
{
    public class Invoice
    {
        public string Line(Order order) => "Total: " + order.GetTotal();

        public decimal Tax(Order order) => (order?.GetTotal() ?? 0) * 0.2m;
    }
}
