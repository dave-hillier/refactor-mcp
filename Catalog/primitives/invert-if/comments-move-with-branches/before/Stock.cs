namespace Shop
{
    public class Stock
    {
        public string Status(int level)
        {
            // Decide what to show.
            /*^*/if (IsLow(level))
            {
                // Ask for more.
                return "reorder";
            }
            else
            {
                // Nothing to do.
                return "ok";
            }
        }

        private bool IsLow(int level) => level < 5;
    }
}
