namespace Shop
{
    public class Order
    {
        internal const decimal Rate = 0.2m;

        public static decimal Vat(decimal net) => TaxRules.Vat(net);

        internal static decimal Round(decimal value) => decimal.Round(value, 2);
    }
}
