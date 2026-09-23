namespace Shop
{
    public class Account
    {
        private decimal _balance;
        private int _lastError = 3;

        public int Withdraw(decimal amount)
        {
            if (amount > _balance)
                return _lastError;

            _balance -= amount;
            return 0;
        }
    }
}
