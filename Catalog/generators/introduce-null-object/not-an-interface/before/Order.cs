namespace Shop
{
    public class Logger
    {
        public void Log(string message)
        {
        }
    }

    public class Order
    {
        private readonly Logger _logger;

        public Order(Logger logger)
        {
            _logger = logger;
        }

        public void Submit()
        {
            _logger?.Log("Submitted");
        }
    }
}
