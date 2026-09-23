namespace Shop
{
    public class Address
    {
        public string Street { get; set; }

        public string Town { get; set; }

        /// <summary>The customer as written on an envelope.</summary>
        // Street and town go on separate lines.
        public string Label(Customer customer)
        {
            return customer.Name + "\n" + Street + "\n" + Town;
        }
    }
}
