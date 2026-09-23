namespace Shop
{
    public class Order
    {
        private const decimal Rate = 0.2m;

        public decimal Tax(decimal amount) => amount * Rate;

        public decimal Gross(decimal net) => net + Tax(net);
    }
}
