namespace Shop
{
    public class Order
    {
        private IDiscountPolicy _policy;

        public int Rank() => _policy?.Priority() ?? -1;

        public int Weight() => _policy != null ? _policy.Priority() : 0;
    }
}
