namespace Shop.Billing
{
    public static class Tax
    {
        public const decimal Rate = 0.2m;

        public static decimal On(decimal amount) => amount * Rate;
    }
}
