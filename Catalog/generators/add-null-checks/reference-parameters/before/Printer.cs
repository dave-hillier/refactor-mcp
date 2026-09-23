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
            return heading + customer.Name + copies + footer;
        }
    }
}
