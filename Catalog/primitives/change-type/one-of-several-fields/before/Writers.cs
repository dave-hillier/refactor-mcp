namespace Shop
{
    public interface IWriter
    {
        void Write(string text);
    }

    public class FileWriter : IWriter
    {
        public void Write(string text)
        {
        }

        public void Flush()
        {
        }
    }
}
