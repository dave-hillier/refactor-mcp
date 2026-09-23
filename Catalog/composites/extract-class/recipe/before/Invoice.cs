namespace Shop
{
    public class Invoice
    {
        public string Header(Customer customer) => customer.Name + "\n" + customer.FormatAddress();
    }
}
