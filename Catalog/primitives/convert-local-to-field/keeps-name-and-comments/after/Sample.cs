using System;

public class Sample
{
    private string mode;

    public void Run(bool fast)
    {
        // Chosen below.
        if (fast)
        {
            mode = "fast";
        }
        else
        {
            mode = "slow";
        }

        Console.WriteLine(mode);
    }
}
