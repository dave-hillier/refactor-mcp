using System.Threading.Tasks;

public class Sample
{
    public async Task Run()
    {
        await Wait();
    }

    private async Task Wait()
    {
        await Task.Delay(1);
    }
}
