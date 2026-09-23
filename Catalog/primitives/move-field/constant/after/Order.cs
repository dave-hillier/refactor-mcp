namespace Shop
{
    public class Order
    {
        public decimal Net { get; set; }

        public decimal Tax() => Net * TaxRules.Rate;
    }

    public static class TaxRules
    {
        internal const decimal Rate = 0.2m;
    }
}
