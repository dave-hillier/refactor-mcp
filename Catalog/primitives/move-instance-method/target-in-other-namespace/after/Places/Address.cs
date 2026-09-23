using System.Linq;

namespace Shop.Places
{
    public class Address
    {
        public string Town { get; set; }

        public string Summary(Customer customer) => Customer.Format(Town) + ": " + customer.Orders.Count(o => o.Total > 0);
    }
}
