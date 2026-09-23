namespace Shop
{
    public class Customer
    {
        public string Label() => Street ?? "no address";

        public string? Street { get; set; }
    }

    public class Order
    {
        public decimal Total { get; set; }
    }
}
