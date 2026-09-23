namespace Staff
{
    public class Disability
    {
        private int _seniority;
        private bool _isPartTime;
        private decimal _baseAmount;

        public decimal Amount()
        {
            // Too new to qualify.
            /*^*/if (_seniority < 2)
            {
                return 0; // nothing
            }

            // Part-time staff are not covered.
            if (_isPartTime)
            {
                return 0;
            }

            return _baseAmount * 0.1m;
        }
    }
}
