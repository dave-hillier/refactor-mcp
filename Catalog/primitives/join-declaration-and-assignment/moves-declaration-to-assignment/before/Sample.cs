using System;

public class Sample
{
    public void Run(int count)
    {
        // How many were written.
        int /*^*/written;
        Console.WriteLine("Starting");

        written = Write(count); // after writing
        Console.WriteLine(written);
    }

    private int Write(int count) => count;
}
