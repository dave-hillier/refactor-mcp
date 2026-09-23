using System;

public class Sample
{
    public void Run(bool fast)
    {
        // Chosen below.
        string /*^*/mode;
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
