using System.Threading.Tasks;

namespace Stock
{
    public class Inventory
    {
        public int Available(string sku, out bool known)
        {
            known = true;
            return Task.FromResult(sku.Length).Result;
        }
    }
}
