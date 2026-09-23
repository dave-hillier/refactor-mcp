using System;
using System.Threading.Tasks;

namespace Stock
{
    public class Inventory
    {
        public int Available(string sku)
        {
            return Task.FromResult(sku.Length).Result;
        }

        public Func<string, int> Counter()
        {
            return Available;
        }
    }
}
