namespace Shop
{
    public class Door
    {
        private bool _closed;
        private int _warnings;

        public int Close()
        {
            // Closing twice is a mistake.
            if (_closed)
                return 1; // already closed

            _closed = true;

            // Closed now.
            return 0;
        }

        public void Leave()
        {
            // Close before leaving.
            if (Close() != 0)
            {
                // Someone closed it already.
                _warnings++;
            }

            _closed = false;
        }
    }
}
