namespace Shop
{
    public class Audit
    {
        public int Record(Account account)
        {
            var code = account.Withdraw(5m);
            return code;
        }
    }
}
