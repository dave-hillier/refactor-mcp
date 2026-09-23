using System.Collections.Generic;

namespace Shop
{
    public class Checkout
    {
        public List<Invoice> Issued { get; } = new List<Invoice>();

        public Invoice Bill(Customer customer, decimal amount)
        {
            var invoice = new Invoice { Customer = customer, Amount = amount };
            Issued.Add(invoice);
            return invoice;
        }
    }
}
