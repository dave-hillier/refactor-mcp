using System.IO;

public class Sample
{
    public string Read(string path)
    {
        using var reader = new StreamReader(path);
        var text = reader.ReadToEnd();
        return text.Trim();
    }
}
