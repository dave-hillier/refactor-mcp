using System;
using System.Threading.Tasks;

namespace Data
{
    public class Database
    {
        public Task SaveAsync()
        {
            return Task.CompletedTask;
        }
    }

    public class Repository
    {
        private readonly Database _database = new Database();

        public async Task Save()
        {
            // Flush before anything else reads.
            await _database.SaveAsync();
        }

        public async Task SaveTwice()
        {
            await Save();
            await Save();
        }

        public Action Later()
        {
            return () => Save().GetAwaiter().GetResult();
        }
    }
}
