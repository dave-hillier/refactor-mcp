using System.Threading.Tasks;

namespace Stock
{
    public class Store
    {
        public Task<int> CountAsync(string sku)
        {
            return Task.FromResult(sku.Length);
        }
    }

    public class Inventory
    {
        private readonly Store _store = new Store();

        public int Available(string sku)
        {
            var count = _store.CountAsync(sku).Result;
            return count > 0 ? count - 1 : 0;
        }

        public bool InStock(string sku)
        {
            return Available(sku) > 0;
        }
    }
}
