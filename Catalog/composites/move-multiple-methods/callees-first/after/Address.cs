namespace Shop
{
    public class Address
    {
        public int Number { get; set; }

        public string Road { get; set; }

        public string Town { get; set; }

        // The house number goes first.
        public string Street() => Number + " " + Road;

        public string Label(Customer customer) => customer.Name + ", " + customer.Street() + ", " + Town;
    }
}
