namespace Shop
{
    public class Greeter
    {
        public Customer Create() => new Customer { Nickname = "Ada" };

        public int Length(Customer customer) => customer.Nickname?.Length ?? 0;
    }
}
