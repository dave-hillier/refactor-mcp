using System.IO;

public class Sample
{
    public void Copy(string from, string to)
    {
        using FileStream source = File.OpenRead(from), target = File.Create(to);
        source.CopyTo(target);
    }
}
