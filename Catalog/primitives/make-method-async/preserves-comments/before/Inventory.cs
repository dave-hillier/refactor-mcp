using System;

namespace Stock
{
    public class Inventory
    {
        private readonly Store _store = new Store();

        // Counts what is left after the reserve.
        public int Available(string sku)
        {
            // Ask the store first.
            var count = _store.CountAsync(sku).Result; // blocks
            Func<int> recount = () => _store.CountAsync(sku).Result;
            return count - recount();
        }
    }
}
