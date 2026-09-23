using System.Threading.Tasks;

public class Sample
{
    public async Task<int> CountAsync(int times)
    {
        var count = 0;
        for (var i = 0; i < times; i++)
            await StepAsync();
        return count;

        async Task /*^*/StepAsync()
        {
            await Task.Yield();
            count++;
        }
    }
}
