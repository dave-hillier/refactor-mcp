namespace Shop
{
    public static class Rates
    {
        public const decimal Vat = 0.2m;

        // The rate most goods pay.
        public static readonly decimal Standard = Vat;

        public static decimal Tax(decimal net) => net * Standard;
    }
}
