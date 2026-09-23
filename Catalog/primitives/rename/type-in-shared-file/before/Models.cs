namespace Shop
{
    public class Customer
    {
        public Address Home { get; set; } = new Address();
    }

    public class Address
    {
        public string Street { get; set; } = "";
    }
}
