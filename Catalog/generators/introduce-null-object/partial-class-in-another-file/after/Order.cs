namespace Shop
{
    public partial class Order
    {
        private ILogger _logger;

        public Order(ILogger logger)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        public Order()
            : this(null)
        {
        }
    }
}
