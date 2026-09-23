// Accounts held in the general ledger.
using System.Collections.Generic;

namespace Shop
{
    /// <summary>An account and its postings.</summary>
    public class Account
    {
        private readonly List<decimal> _postings = new List<decimal>();

        public void Post(decimal amount) => _postings.Add(amount);
    }
}
