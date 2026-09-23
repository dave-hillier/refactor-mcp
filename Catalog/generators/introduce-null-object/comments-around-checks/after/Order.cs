namespace Shop
{
    public class Order
    {
        private readonly ILogger _logger;
        private int _saved;

        public Order(ILogger logger)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        public void Submit()
        {
            // Tell the log first.
            // Before saving
            _logger.Log("Submitting"); // not yet saved

            _saved++;

            #region Audit
            _logger.Log("Saved");
            #endregion
        }
    }
}
