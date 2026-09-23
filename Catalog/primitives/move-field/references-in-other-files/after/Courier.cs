namespace Shop
{
    public class Courier
    {
        public string Route(Customer customer) => customer.Address.Town + " " + customer.Address.Postcode;

        public void Correct(Customer customer, string postcode) => customer.Address.Postcode = postcode.ToUpperInvariant();
    }
}
