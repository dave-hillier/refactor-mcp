namespace Shop
{
    public class Teller
    {
        public string Pay(Account account, decimal amount)
        {
            if (account.Withdraw(amount) != 0)
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
