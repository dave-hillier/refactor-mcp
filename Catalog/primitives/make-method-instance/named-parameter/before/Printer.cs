namespace Shop
{
    public class Printer
    {
        public Order Current { get; set; }

        public string Print(Order order) => Order.Label("No. ", order);

        public string PrintCurrent() => Order.Label("Now: ", Current ?? new Order());
    }
}
