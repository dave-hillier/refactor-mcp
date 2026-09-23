using System.IO;

namespace Shop
{
    public class FileWriter
    {
        private readonly string _path;

        public FileWriter(string path)
        {
            _path = path;
        }

        /// <summary>Appends the text to the file.</summary>
        public void Write(string text) => File.AppendAllText(_path, text);

        public void Flush()
        {
        }
    }
}
