namespace Shop
{
    public class Courier
    {
        public string Route(Customer customer) => customer.Address.Town + " " + customer.Postcode;

        public void Correct(Customer customer, string postcode) => customer.Postcode = postcode.ToUpperInvariant();
    }
}
