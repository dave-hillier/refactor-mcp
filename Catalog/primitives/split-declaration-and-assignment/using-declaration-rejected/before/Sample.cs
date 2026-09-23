using System.IO;

public class Sample
{
    public int Length(string text)
    {
        using var /*^*/reader = new StringReader(text);
        return reader.ReadToEnd().Length;
    }
}
