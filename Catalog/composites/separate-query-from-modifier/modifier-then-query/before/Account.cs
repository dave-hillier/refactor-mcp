namespace Bank
{
    public class Account
    {
        private decimal _balance;
        private int _withdrawals;

        public Account(decimal balance)
        {
            _balance = balance;
        }

        public decimal Withdraw(decimal amount)
        {
            _balance -= amount;
            _withdrawals++;
            return _balance;
        }

        public int Withdrawals()
        {
            return _withdrawals;
        }
    }
}
