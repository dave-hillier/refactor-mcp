namespace Shop
{
    public interface IStream : IReader
    {
        new string Current { get; }

        void Write(string text);
    }
}
