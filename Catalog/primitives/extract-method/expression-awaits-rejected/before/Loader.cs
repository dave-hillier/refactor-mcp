using System.Threading.Tasks;

namespace Data
{
    public class Loader
    {
        public async Task<int> Total()
        {
            var total = /*[*/await Task.FromResult(1) + 2/*]*/;
            return total;
        }
    }
}
