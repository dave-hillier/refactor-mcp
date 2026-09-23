namespace Bank
{
    public class Account
    {
        private decimal _balance;

        public decimal Withdraw(decimal amount)
        {
            _balance -= amount;
            return _balance;
        }

        public decimal Twice()
        {
            return Withdraw(5m) * 2;
        }
    }
}
