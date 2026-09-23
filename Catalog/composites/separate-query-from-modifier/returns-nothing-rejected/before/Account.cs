namespace Bank
{
    public class Account
    {
        private decimal _balance;

        public void Deposit(decimal amount)
        {
            _balance += amount;
        }
    }
}
