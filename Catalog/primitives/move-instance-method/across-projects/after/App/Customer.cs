using Core;

namespace App
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public string Town() => _address.Town();
    }
}
