namespace Shop
{
    public class Order
    {
        private IDiscountPolicy _policy;

        public void Use(IDiscountPolicy policy)
        {
            _policy = policy;
        }

        public void Clear()
        {
            _policy = null;
        }

        public decimal Discount(decimal total) => _policy != null ? _policy.Rate(total) : 0m;

        public decimal Final(decimal total)
        {
            return total - (_policy == null ? 0m : _policy.Rate(total));
        }

        public string Describe() => _policy?.Name ?? "none";

        public int Rank() => _policy?.Priority() ?? -1;
    }
}
