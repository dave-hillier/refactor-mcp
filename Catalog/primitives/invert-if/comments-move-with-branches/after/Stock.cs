namespace Shop
{
    public class Stock
    {
        public string Status(int level)
        {
            // Decide what to show.
            if (!IsLow(level))
            {
                // Nothing to do.
                return "ok";
            }
            else
            {
                // Ask for more.
                return "reorder";
            }
        }

        private bool IsLow(int level) => level < 5;
    }
}
