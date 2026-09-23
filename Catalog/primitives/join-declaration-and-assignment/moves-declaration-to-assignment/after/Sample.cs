using System;

public class Sample
{
    public void Run(int count)
    {
        Console.WriteLine("Starting");

        // How many were written.
        int written = Write(count); // after writing
        Console.WriteLine(written);
    }

    private int Write(int count) => count;
}
