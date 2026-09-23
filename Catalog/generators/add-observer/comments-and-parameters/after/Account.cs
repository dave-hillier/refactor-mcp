using System;

namespace Shop
{
    public class Account
    {
        public decimal Balance { get; private set; }

        public event Action<Account, decimal, string> Transferred;

        /// <summary>Moves money to another account.</summary>
        public void Transfer(Account to, decimal amount, string reference)
        {
            Balance -= amount;
            to.Balance += amount;
            // done
            Transferred?.Invoke(to, amount, reference);
        }
    }
}
