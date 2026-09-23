namespace Shop
{
    public static class Rates
    {
        public const decimal Vat = 0.2m;

        public static decimal Tax(decimal net) => net * Vat;
    }
}
