using System.Threading.Tasks;

public class Sample
{
    public async Task SaveAsync()
    {
        await Task.Delay(10);
    }

    public async Task<int> CountAsync() => await Task.FromResult(3);
}
