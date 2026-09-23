using System.Threading.Tasks;

namespace Stock
{
    public class Inventory
    {
        public Task<int> Available(string sku)
        {
            return Task.FromResult(Task.FromResult(sku.Length).Result);
        }
    }
}
