namespace Shop
{
    public class Greeter
    {
        public Customer Create() => new Customer { DisplayName = "Ada" };

        public int Length(Customer customer) => customer.DisplayName?.Length ?? 0;
    }
}
