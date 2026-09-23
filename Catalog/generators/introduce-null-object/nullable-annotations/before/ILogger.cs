namespace Shop
{
    public interface ILogger
    {
        string Name { get; }

        void Log(string message, string? category = null);

        string? LastMessage();

        string Format(string message);
    }
}
