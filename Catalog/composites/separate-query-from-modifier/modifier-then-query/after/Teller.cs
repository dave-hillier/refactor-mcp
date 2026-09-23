namespace Bank
{
    public class Teller
    {
        public decimal Serve(Account account)
        {
            account.Debit(10m);
            var left = account.Balance();
            if (left < 0)
            {
                account.Debit(-10m);
                return account.Balance();
            }
            return left;
        }
    }
}
