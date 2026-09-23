namespace Shop
{
    public class Address
    {
        public string Town { get; set; }

        public string Label(Customer customer) => customer.Name + ", " + Town;
    }
}
