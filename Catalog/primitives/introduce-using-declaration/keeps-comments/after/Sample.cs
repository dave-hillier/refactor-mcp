using System;
using System.IO;

public class Sample
{
    public void Copy(string from, string to)
    {
        Console.WriteLine("Copying");

        // Both files stay open until the copy ends.
        using var source = File.OpenRead(from); // read side
        // Truncates any existing file.
        using var target = File.Create(to);

        source.CopyTo(target);
    }
}
