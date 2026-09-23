namespace Shop
{
    public class Order
    {
        private ILogger _logger;

        public Order(ILogger? logger)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        public void Detach()
        {
            _logger = NullLogger.Instance;
        }

        public void Submit()
        {
            _logger.Log("Submitted");
        }

        public string LoggerName() => _logger.Name;
    }
}
