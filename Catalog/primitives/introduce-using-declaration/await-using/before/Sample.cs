using System;
using System.Threading.Tasks;

public sealed class Connection : IAsyncDisposable
{
    public Task SendAsync(string message) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class Sample
{
    public async Task SendAsync(string message)
    {
        /*^*/await using (var connection = new Connection())
        {
            await connection.SendAsync(message);
        }
    }
}
