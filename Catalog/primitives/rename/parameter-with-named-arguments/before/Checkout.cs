namespace Shop
{
    public class Checkout
    {
        public decimal Total(decimal amount) =>
            Pricing.Apply(amount, factor: 0.9m) + Pricing.Apply(factor: 2m, amount: amount);
    }
}
