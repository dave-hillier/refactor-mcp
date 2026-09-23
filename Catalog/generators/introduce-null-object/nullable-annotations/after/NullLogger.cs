namespace Shop
{
    public sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new NullLogger();

        private NullLogger()
        {
        }

        public string Name => "unnamed";

        public void Log(string message, string? category = null)
        {
        }

        public string? LastMessage()
        {
            return default;
        }

        public string Format(string message)
        {
            return default!;
        }
    }
}
