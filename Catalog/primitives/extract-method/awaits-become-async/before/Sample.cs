using System;
using System.Threading.Tasks;

public class Sample
{
    public async Task Run(int delay)
    {
        /*[*/await Task.Delay(delay);
        Console.WriteLine("Waited");/*]*/
        Console.WriteLine("Done");
    }
}
