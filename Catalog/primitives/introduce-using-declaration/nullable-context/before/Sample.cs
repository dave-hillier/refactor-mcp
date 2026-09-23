using System.IO;

public class Sample
{
    public string? FirstLine(string? path)
    {
        /*^*/using (StreamReader? reader = path is null ? null : new StreamReader(path))
        {
            return reader?.ReadLine();
        }
    }
}
