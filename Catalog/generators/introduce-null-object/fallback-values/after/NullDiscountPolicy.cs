namespace Shop
{
    public sealed class NullDiscountPolicy : IDiscountPolicy
    {
        public static readonly NullDiscountPolicy Instance = new NullDiscountPolicy();

        private NullDiscountPolicy()
        {
        }

        public string Name => "none";

        public decimal Rate(decimal total)
        {
            return 0m;
        }

        public int Priority()
        {
            return -1;
        }
    }
}
