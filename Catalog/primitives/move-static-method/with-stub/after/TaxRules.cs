namespace Shop
{
    public static class TaxRules
    {
        public const string Region = "UK";

        // VAT is charged on the net amount.
        public static decimal Vat(decimal net) => Order.Round(net * Order.Rate);
    }
}
