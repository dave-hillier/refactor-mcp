using System.Threading.Tasks;

namespace Data
{
    public class Cache
    {
        public Task<T> GetAsync<T>(string key) => Task.FromResult(default(T));
    }

    public class Settings
    {
        private readonly Cache _cache = new Cache();

        public async Task<T> Load<T>(string key) => await _cache.GetAsync<T>(key);

        public int Timeout() => Load<int>("timeout").GetAwaiter().GetResult() * 1000;
    }
}
