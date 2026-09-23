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

        public async Task<int> Available(string sku)
        {
            var count = await _store.CountAsync(sku);
            return count > 0 ? count - 1 : 0;
        }

        public async Task<bool> InStock(string sku)
        {
            return await Available(sku) > 0;
        }
    }
}
