namespace Catalog
{
    public class Customer
    {
        public Invoice Latest { get; set; }

        public InvoiceState State => InvoiceState.Draft;
    }
}
