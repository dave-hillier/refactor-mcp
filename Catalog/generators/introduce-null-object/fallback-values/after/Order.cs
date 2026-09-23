namespace Shop
{
    public class Order
    {
        private IDiscountPolicy _policy = NullDiscountPolicy.Instance;

        public void Use(IDiscountPolicy policy)
        {
            _policy = policy ?? NullDiscountPolicy.Instance;
        }

        public void Clear()
        {
            _policy = NullDiscountPolicy.Instance;
        }

        public decimal Discount(decimal total) => _policy.Rate(total);

        public decimal Final(decimal total)
        {
            return total - _policy.Rate(total);
        }

        public string Describe() => _policy.Name;

        public int Rank() => _policy.Priority();
    }
}
