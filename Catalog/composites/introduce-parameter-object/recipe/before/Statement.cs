using System;

namespace Bank
{
    public class Statement
    {
        public decimal Monthly(Account account, DateTime from, DateTime to)
        {
            return account.TotalBetween(from, to);
        }
    }
}
