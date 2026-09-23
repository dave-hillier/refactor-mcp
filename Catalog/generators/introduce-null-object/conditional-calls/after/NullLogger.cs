namespace Shop
{
    public sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new NullLogger();

        private NullLogger()
        {
        }

        public void Log(string message)
        {
        }
    }
}
