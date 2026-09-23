using System.Collections.Generic;

namespace Accounts
{
    public class Ledger
    {
        private readonly List<int> _rejected = new List<int>();

        public void Record(int amount)
        {
            /*^*/if (amount < 0)
            {
                _rejected.Add(amount);
            }
        }
    }
}
