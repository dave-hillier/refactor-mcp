using System;

namespace Shop
{
    public class Customer
    {
        public string Name { get; set; }
    }

    public class Printer
    {
        public string Print(Customer customer, int copies, string heading, string footer = null)
        {
            ArgumentNullException.ThrowIfNull(customer);
            ArgumentNullException.ThrowIfNull(heading);

            return heading + customer.Name + copies + footer;
        }
    }
}
