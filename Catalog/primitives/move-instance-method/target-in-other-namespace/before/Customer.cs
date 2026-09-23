using System.Collections.Generic;
using System.Linq;
using Shop.Places;

namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public List<Order> Orders { get; } = new List<Order>();

        public string Summary() => Format(_address.Town) + ": " + Orders.Count(o => o.Total > 0);

        private static string Format(string text) => text.Trim();
    }

    public class Order
    {
        public decimal Total { get; set; }
    }
}
