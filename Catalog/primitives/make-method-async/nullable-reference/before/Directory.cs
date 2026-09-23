using System.Threading.Tasks;

namespace Data
{
    public class Directory
    {
        public Task<string?> FindAsync(string key) => Task.FromResult<string?>(null);

        public string? Find(string key)
        {
            return FindAsync(key).Result;
        }

        public string Describe(string key)
        {
            return Find(key) ?? "none";
        }
    }
}
