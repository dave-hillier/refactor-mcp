namespace Shop
{
    public class Account
    {
        public decimal Rebate { get; set; }

        internal decimal Discounted(Customer customer, decimal amount) => amount * (1 - customer._loyalty - Rebate);
    }
}
