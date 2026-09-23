namespace Shop
{
    public class Order
    {
        private readonly ILogger _logger;
        private int _saved;

        public Order(ILogger logger)
        {
            _logger = logger;
        }

        public void Submit()
        {
            // Tell the log first.
            if (_logger != null)
            {
                // Before saving
                _logger.Log("Submitting"); // not yet saved
            }

            _saved++;

            #region Audit
            if (_logger != null) _logger.Log("Saved");
            #endregion
        }
    }
}
