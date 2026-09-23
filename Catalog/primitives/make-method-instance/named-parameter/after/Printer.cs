namespace Shop
{
    public class Printer
    {
        public Order Current { get; set; }

        public string Print(Order order) => order.Label("No. ");

        public string PrintCurrent() => (Current ?? new Order()).Label("Now: ");
    }
}
