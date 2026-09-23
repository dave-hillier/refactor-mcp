using System.Threading.Tasks;

namespace Stock
{
    public class Inventory
    {
        public virtual int Available(string sku)
        {
            return sku.Length;
        }
    }

    public class CachedInventory : Inventory
    {
        public override int Available(string sku)
        {
            return Task.FromResult(sku.Length).Result;
        }
    }
}
