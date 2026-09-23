namespace Staff
{
    public class Disability
    {
        private int _seniority;
        private bool _isPartTime;
        private decimal _baseAmount;

        public decimal Amount()
        {
            // New starters are not covered.
            /*^*/if (_seniority < 2)
            {
                return 0;
            }

            // Nor are part-timers.
            if (_isPartTime)
            {
                return 0;
            }

            return _baseAmount * 0.1m;
        }
    }
}
