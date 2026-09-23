using System;
using System.IO;

public class Sample
{
    public void Write(string path, bool log)
    {
        if (log)
        {
            var writer = Console.Out;
            writer.WriteLine("Writing");
        }

        /*^*/using (var writer = new StreamWriter(path))
        {
            writer.Write("text");
        }
    }
}
