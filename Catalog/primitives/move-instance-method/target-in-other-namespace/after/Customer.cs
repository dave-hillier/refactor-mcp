using System.Collections.Generic;
using Shop.Places;

namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public List<Order> Orders { get; } = new List<Order>();

        public string Summary() => _address.Summary(this);

        internal static string Format(string text) => text.Trim();
    }

    public class Order
    {
        public decimal Total { get; set; }
    }
}
