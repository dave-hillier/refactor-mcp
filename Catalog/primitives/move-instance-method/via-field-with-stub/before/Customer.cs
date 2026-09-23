namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public string Name { get; set; }

        /// <summary>The customer as written on an envelope.</summary>
        // Street and town go on separate lines.
        public string Label()
        {
            return Name + "\n" + _address.Street + "\n" + this._address.Town;
        }
    }
}
