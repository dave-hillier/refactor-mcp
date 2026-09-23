using System;
using System.Threading.Tasks;

namespace Stock
{
    public class Inventory
    {
        private readonly Store _store = new Store();

        // Counts what is left after the reserve.
        public async Task<int> Available(string sku)
        {
            // Ask the store first.
            var count = await _store.CountAsync(sku); // blocks
            Func<int> recount = () => _store.CountAsync(sku).Result;
            return count - recount();
        }
    }
}
