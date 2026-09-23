using System.IO;

public class Sample
{
    public void Write(string path, bool enabled)
    {
        if (enabled)
            /*^*/using (var writer = new StreamWriter(path))
            {
                writer.Write("text");
            }
    }
}
