namespace Shop
{
    public class Courier
    {
        public void Deliver(Customer customer) => customer.Address.Street = customer.Address.Street.Trim();
    }
}
