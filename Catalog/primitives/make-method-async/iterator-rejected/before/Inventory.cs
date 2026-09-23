using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stock
{
    public class Inventory
    {
        public IEnumerable<int> Available(string sku)
        {
            yield return Task.FromResult(sku.Length).Result;
        }
    }
}
