using System;
using System.Threading.Tasks;

public class Sample
{
    public async Task Run(int delay)
    {
        await Pause(delay);
        Console.WriteLine("Done");
    }

    private async Task Pause(int delay)
    {
        await Task.Delay(delay);
        Console.WriteLine("Waited");
    }
}
