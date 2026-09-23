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
            // Part-time staff are not covered.
            if (_seniority < 2 || _isPartTime)
            {
                return 0; // nothing
            }

            return _baseAmount * 0.1m;
        }
    }
}
