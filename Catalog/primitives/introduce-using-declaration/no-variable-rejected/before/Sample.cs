using System.IO;

public class Sample
{
    public void Touch(string path)
    {
        /*^*/using (File.Create(path))
        {
        }
    }
}
