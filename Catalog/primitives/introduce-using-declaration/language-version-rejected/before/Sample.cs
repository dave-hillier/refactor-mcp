using System.IO;

public class Sample
{
    public string Read(string path)
    {
        /*^*/using (var reader = new StreamReader(path))
        {
            return reader.ReadToEnd();
        }
    }
}
