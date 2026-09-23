using IO = System.IO;

namespace Shop
{
    public class Files
    {
        public bool Exists(string path) => IO.File.Exists(path);
    }
}
