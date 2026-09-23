namespace Shop
{
    public class Envelope
    {
        public string Front(Customer customer) => customer.Label();
    }
}
