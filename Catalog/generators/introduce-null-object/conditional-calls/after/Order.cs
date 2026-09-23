namespace Shop
{
    public class Order
    {
        private readonly ILogger _logger;

        public Order(ILogger logger)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        public void Submit()
        {
            _logger.Log("Submitting");
            _logger.Log("Submitted");
        }

        public void Cancel()
        {
            _logger.Log("Cancelled");
        }
    }
}
