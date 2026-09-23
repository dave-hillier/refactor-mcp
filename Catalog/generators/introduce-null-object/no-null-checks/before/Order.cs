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
            _logger.Log("Submitted");
        }
    }
}
