namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public string Name { get; set; }

        /// <summary>The customer as written on an envelope.</summary>
        public string Label()
        {
            return _address.Label(this);
        }
    }
}
