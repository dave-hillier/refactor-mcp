using System;

namespace Shop
{
    public class Account
    {
        private decimal _balance;

        public void Withdraw(decimal amount)
        {
            if (amount <= 0)
                throw new InvalidOperationException("Withdraw returned error code 1");
            if (amount > _balance)
                throw new InvalidOperationException("Withdraw returned error code 2");

            _balance -= amount;
        }
    }
}
