namespace Shop
{
    public static class Pricing
    {
        public static decimal Apply(decimal amount, decimal rate = 1m)
        {
            return amount * rate;
        }
    }
}
