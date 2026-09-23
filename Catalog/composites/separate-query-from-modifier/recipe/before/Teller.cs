namespace Bank
{
    public class Teller
    {
        public decimal Serve(Account account)
        {
            var left = account.Withdraw(10m);
            if (left < 0)
                return account.Withdraw(-10m);
            return left;
        }
    }
}
