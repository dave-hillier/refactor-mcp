using System.Collections.Generic;

namespace Accounts
{
    public class Ledger
    {
        private readonly List<int> _rejected = new List<int>();
        private readonly List<int> _accepted = new List<int>();
        private int _limit = 100;

        public void Record(int amount)
        {
            if (OutOfRange(amount))
                Reject(amount);
            else
                _accepted.Add(amount);
        }

        private bool OutOfRange(int amount)
        {
            return amount < 0 || amount > _limit;
        }

        private void Reject(int amount)
        {
            _rejected.Add(amount);
        }
    }
}
