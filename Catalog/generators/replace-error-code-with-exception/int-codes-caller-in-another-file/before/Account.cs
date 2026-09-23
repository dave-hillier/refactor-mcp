namespace Shop
{
    public class Account
    {
        private decimal _balance;

        public int Withdraw(decimal amount)
        {
            if (amount <= 0)
                return 1;
            if (amount > _balance)
                return 2;

            _balance -= amount;
            return 0;
        }
    }
}
