using System;
using System.IO;

public class Sample
{
    public void Append(string path, string line, bool enabled)
    {
        if (enabled)
        {
            using var writer = File.AppendText(path);
            writer.WriteLine(line);
        }

        Console.WriteLine("Done");
    }
}
