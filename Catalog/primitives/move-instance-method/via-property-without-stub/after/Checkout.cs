namespace Shop
{
    public class Checkout
    {
        public string Print(Customer customer) => customer.Address.Label(customer);
    }
}
