using System;

namespace Shop
{
    public class Teller
    {
        public string Pay(Account account, decimal amount)
        {
            try
            {
                account.Withdraw(amount);
            }
            catch (InvalidOperationException)
            {
                return "Declined";
            }

            return "Paid";
        }

        public void Drain(Account account)
        {
            account.Withdraw(100m);
        }
    }
}
