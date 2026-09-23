using System.IO;

public class Sample
{
    public void Write(int kind, string path)
    {
        switch (kind)
        {
            case 1:
                /*^*/using (var writer = new StreamWriter(path))
                {
                    writer.Write("one");
                }
                break;
        }
    }
}
