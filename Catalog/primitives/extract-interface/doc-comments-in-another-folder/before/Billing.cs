using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shop
{
    public class Billing
    {
        private readonly List<decimal> _amounts = new List<decimal>();

        /// <summary>
        /// The amounts billed so far.
        /// </summary>
        public IReadOnlyList<decimal> Amounts => _amounts;

        /// <summary>Totals every amount.</summary>
        public Task<decimal> TotalAsync() => Task.FromResult(_amounts.Sum());
    }
}
