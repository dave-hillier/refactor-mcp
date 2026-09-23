namespace Shop
{
    public class Invoice
    {
        public decimal Gross(decimal net) => net + net * Rates.Standard;
    }
}
