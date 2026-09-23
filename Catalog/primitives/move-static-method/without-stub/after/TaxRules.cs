namespace Shop
{
    public static class TaxRules
    {
        public static decimal Vat(decimal net) => net * 0.2m;
    }
}
