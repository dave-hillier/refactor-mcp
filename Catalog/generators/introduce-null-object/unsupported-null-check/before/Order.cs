namespace Shop
{
    public class Order
    {
        private readonly ILogger _logger;
        private int _submitted;

        public Order(ILogger logger)
        {
            _logger = logger;
        }

        public void Submit()
        {
            _logger?.Log("Submitting");
        }

        public void Count()
        {
            if (_logger == null)
                return;

            _submitted++;
        }
    }
}
