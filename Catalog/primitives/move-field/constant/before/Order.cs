namespace Shop
{
    public class Order
    {
        private const decimal Rate = 0.2m;

        public decimal Net { get; set; }

        public decimal Tax() => Net * Rate;
    }

    public static class TaxRules
    {
    }
}
