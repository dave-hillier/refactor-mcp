namespace Billing
{
    public static class Tax
    {
        private const decimal Rate = 0.2m;

        public static decimal Total(decimal amount)
        {
            return amount + Levy(amount);
        }

        private static decimal Levy(decimal amount)
        {
            return amount * Rate;
        }
    }
}
