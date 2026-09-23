using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shop
{
    public interface IBilling
    {
        /// <summary>
        /// The amounts billed so far.
        /// </summary>
        IReadOnlyList<decimal> Amounts { get; }
        /// <summary>Totals every amount.</summary>
        Task<decimal> TotalAsync();
    }
}
