#nullable enable

namespace Shop
{
    public class Order
    {
        private readonly AddressBook _book = new AddressBook();

        public string? Find(string? prefix) => _book.Find(prefix);
    }
}
