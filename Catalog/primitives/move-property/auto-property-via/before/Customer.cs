namespace Shop
{
    public class Customer
    {
        public Address Address { get; } = new Address();

        /// <summary>The first line of the address.</summary>
        public string Street { get; set; }

        public string Label() => Street + ", " + Address.Town;
    }
}
