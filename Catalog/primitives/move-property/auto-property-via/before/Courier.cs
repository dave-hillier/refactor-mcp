namespace Shop
{
    public class Courier
    {
        public void Deliver(Customer customer) => customer.Street = customer.Street.Trim();
    }
}
