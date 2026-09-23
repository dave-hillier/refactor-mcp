using Core;

namespace App
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public string Name { get; set; }

        public string Label() => Name + ", " + _address.Town;
    }
}
