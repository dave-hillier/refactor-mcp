namespace Shop
{
    public class Account
    {
        private decimal _balance;

        public int Deposit(decimal amount)
        {
            _balance += amount;
            return 0;
        }
    }
}
