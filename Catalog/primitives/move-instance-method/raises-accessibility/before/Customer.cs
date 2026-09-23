namespace Shop
{
    public class Customer
    {
        private readonly Account _account = new Account();
        private decimal _loyalty = 0.05m;

        public decimal Pay(decimal amount) => Discounted(amount);

        private decimal Discounted(decimal amount) => amount * (1 - _loyalty - _account.Rebate);
    }
}
