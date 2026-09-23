namespace Shop
{
    public interface IReader
    {
        object Current { get; }

        string Read();

        string Read(int count);
    }
}
