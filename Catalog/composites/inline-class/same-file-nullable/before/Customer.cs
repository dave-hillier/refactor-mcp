namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new();

        public string Label() => _address.Street ?? "no address";
    }

    public class Address
    {
        public string? Street { get; set; }
    }

    public class Order
    {
        public decimal Total { get; set; }
    }
}
