using System.Collections.Generic;

namespace Shop
{
    public class Statement
    {
        public IEnumerable<string> Lines(Account account)
        {
            if (account.Withdraw(5m) != 0)
            {
                yield return "Declined";
            }

            yield return "Done";
        }
    }
}
