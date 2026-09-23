using System.IO;

public class Sample
{
    public string Read(string path)
    {
        /*^*/var reader = new StreamReader(path);
        return reader.ReadToEnd();
    }
}
