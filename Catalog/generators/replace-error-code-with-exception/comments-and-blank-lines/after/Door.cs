using System;

namespace Shop
{
    public class Door
    {
        private bool _closed;
        private int _warnings;

        public void Close()
        {
            // Closing twice is a mistake.
            if (_closed)
                throw new InvalidOperationException("Close returned error code 1"); // already closed

            _closed = true;

            // Closed now.
            return;
        }

        public void Leave()
        {
            // Close before leaving.
            try
            {
                Close();
            }
            catch (InvalidOperationException)
            {
                // Someone closed it already.
                _warnings++;
            }

            _closed = false;
        }
    }
}
