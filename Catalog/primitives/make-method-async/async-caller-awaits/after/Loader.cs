using System.Threading.Tasks;

namespace Data
{
    public class Loader
    {
        private readonly Task<string> _pending = Task.FromResult("data");

        public async Task<string> Load()
        {
            return await _pending;
        }

        public async Task<int> LengthAsync()
        {
            await Task.Yield();
            return (await Load()).Length;
        }
    }
}
