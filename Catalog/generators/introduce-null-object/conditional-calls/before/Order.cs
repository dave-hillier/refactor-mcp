namespace Shop
{
    public class Order
    {
        private readonly ILogger _logger;

        public Order(ILogger logger)
        {
            _logger = logger;
        }

        public void Submit()
        {
            if (_logger != null)
            {
                _logger.Log("Submitting");
                _logger.Log("Submitted");
            }
        }

        public void Cancel()
        {
            _logger?.Log("Cancelled");
        }
    }
}
