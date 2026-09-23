using System.Threading.Tasks;

namespace Data
{
    public class Loader
    {
        private readonly Task<string> _pending = Task.FromResult("data");

        public string Load()
        {
            return _pending.GetAwaiter().GetResult();
        }

        public async Task<int> LengthAsync()
        {
            await Task.Yield();
            return Load().Length;
        }
    }
}
