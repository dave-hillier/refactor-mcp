using System.IO;

public class Sample
{
    public void Copy(string from, string to)
    {
        using var source = File.OpenRead(from);
        using var target = File.Create(to);
        source.CopyTo(target);
    }
}
