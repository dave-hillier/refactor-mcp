using System.Threading.Tasks;

namespace Stock
{
    public class Inventory
    {
        public int Available(string sku)
        {
            return sku.Length;
        }
    }
}
