namespace Shop
{
    public class Account
    {
        private decimal _balance;

        public int Withdraw(decimal amount)
        {
            if (amount > _balance)
                return 1;

            _balance -= amount;
            return 0;
        }
    }
}
