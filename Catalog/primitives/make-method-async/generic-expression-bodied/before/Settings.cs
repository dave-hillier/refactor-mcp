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

        public T Load<T>(string key) => _cache.GetAsync<T>(key).Result;

        public int Timeout() => Load<int>("timeout") * 1000;
    }
}
