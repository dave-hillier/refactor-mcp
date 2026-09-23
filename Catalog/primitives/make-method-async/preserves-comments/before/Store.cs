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
}
