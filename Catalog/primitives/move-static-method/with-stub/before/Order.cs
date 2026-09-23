namespace Shop
{
    public class Order
    {
        private const decimal Rate = 0.2m;

        // VAT is charged on the net amount.
        public static decimal Vat(decimal net) => Round(net * Rate);

        private static decimal Round(decimal value) => decimal.Round(value, 2);
    }
}
