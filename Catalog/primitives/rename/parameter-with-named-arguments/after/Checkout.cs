namespace Shop
{
    public class Checkout
    {
        public decimal Total(decimal amount) =>
            Pricing.Apply(amount, rate: 0.9m) + Pricing.Apply(rate: 2m, amount: amount);
    }
}
