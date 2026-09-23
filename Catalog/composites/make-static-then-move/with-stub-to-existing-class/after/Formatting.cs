namespace Shop
{
    public class Formatting
    {
        public static string Money(decimal amount) => amount.ToString("0.00");

        /// <summary>A line for the receipt.</summary>
        public static string Describe(Order sale, string prefix)
        {
            // The total is shown without rounding.
            return prefix + sale._total + " " + sale.Currency;
        }
    }
}
