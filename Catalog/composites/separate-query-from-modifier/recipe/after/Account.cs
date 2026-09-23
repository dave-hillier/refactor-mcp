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

        public decimal Balance()
        {
            return _balance;
        }

        public void Debit(decimal amount)
        {
            _balance -= amount;
            _withdrawals++;
        }

        public int Withdrawals()
        {
            return _withdrawals;
        }
    }
}
