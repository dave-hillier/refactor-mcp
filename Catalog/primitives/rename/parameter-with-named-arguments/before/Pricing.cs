namespace Shop
{
    public static class Pricing
    {
        public static decimal Apply(decimal amount, decimal /*^*/factor = 1m)
        {
            return amount * factor;
        }
    }
}
