namespace Shop
{
    public class Customer
    {
        private readonly Account _account = new Account();
        internal decimal _loyalty = 0.05m;

        public decimal Pay(decimal amount) => Discounted(amount);

        private decimal Discounted(decimal amount) => _account.Discounted(this, amount);
    }
}
