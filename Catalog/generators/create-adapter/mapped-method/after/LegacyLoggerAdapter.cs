namespace Shop
{
    public class LegacyLoggerAdapter : ILogger
    {
        private readonly LegacyLogger _adaptee;

        public LegacyLoggerAdapter(LegacyLogger adaptee)
        {
            _adaptee = adaptee;
        }

        public void Log(string message) => _adaptee.Write(message);
    }
}
