using System.Threading.Tasks;

namespace Stock
{
    public class Inventory
    {
        private readonly int _count;

        public Inventory()
        {
            _count = Task.FromResult(3).Result;
        }
    }
}
