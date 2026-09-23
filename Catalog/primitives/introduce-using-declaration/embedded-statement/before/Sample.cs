using System.IO;

public class Sample
{
    public void Write(string path, string text)
    {
        /*^*/using (var writer = new StreamWriter(path))
            writer.Write(text);
    }
}
