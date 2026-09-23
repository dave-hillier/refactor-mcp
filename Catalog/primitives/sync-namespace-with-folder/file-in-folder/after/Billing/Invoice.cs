namespace Catalog.Billing
{
    public class Invoice
    {
        public Customer Customer { get; set; }
    }

    public enum InvoiceState
    {
        Draft,
        Paid,
    }
}
