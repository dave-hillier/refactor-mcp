using System.Threading.Tasks;

namespace Data
{
    public class Directory
    {
        public Task<string?> FindAsync(string key) => Task.FromResult<string?>(null);

        public async Task<string?> Find(string key)
        {
            return await FindAsync(key);
        }

        public string Describe(string key)
        {
            return Find(key).GetAwaiter().GetResult() ?? "none";
        }
    }
}
