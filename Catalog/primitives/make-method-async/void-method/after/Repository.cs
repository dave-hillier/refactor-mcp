using System.Threading.Tasks;

namespace Data
{
    public class Connection
    {
        public Task WriteAsync(string row) => Task.CompletedTask;
    }

    public class Repository
    {
        private readonly Connection _connection = new Connection();

        public async Task Save(string row)
        {
            await _connection.WriteAsync(row);
        }

        public void SaveAll(string[] rows)
        {
            foreach (var row in rows)
                Save(row).GetAwaiter().GetResult();
        }
    }
}
