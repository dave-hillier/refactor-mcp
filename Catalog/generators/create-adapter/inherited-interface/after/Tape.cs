namespace Shop
{
    public interface IReader
    {
        string Read();
    }

    public interface IStream : IReader
    {
        void Write(string text);
    }

    public class Tape
    {
        private string _recorded = "";

        public string Play() => _recorded;

        public void Record(string text) => _recorded += text;
    }
}
