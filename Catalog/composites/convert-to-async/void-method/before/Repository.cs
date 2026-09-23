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

        public void Save()
        {
            // Flush before anything else reads.
            _database.SaveAsync().Wait();
        }

        public void SaveTwice()
        {
            Save();
            Save();
        }

        public Action Later()
        {
            return () => Save();
        }
    }
}
