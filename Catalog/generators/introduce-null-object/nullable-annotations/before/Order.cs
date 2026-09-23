namespace Shop
{
    public class Order
    {
        private ILogger? _logger;

        public Order(ILogger? logger)
        {
            _logger = logger;
        }

        public void Detach()
        {
            _logger = null;
        }

        public void Submit()
        {
            _logger?.Log("Submitted");
        }

        public string LoggerName() => _logger?.Name ?? "unnamed";
    }
}
