namespace Shop
{
    public class Account
    {
        private decimal _balance;

        public string Withdraw(decimal amount)
        {
            if (amount > _balance)
                return "overdrawn";

            _balance -= amount;
            return null;
        }
    }
}
