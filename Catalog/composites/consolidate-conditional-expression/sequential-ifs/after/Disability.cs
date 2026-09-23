namespace Staff
{
    public class Disability
    {
        private int _seniority;
        private int _monthsDisabled;
        private bool _isPartTime;
        private decimal _baseAmount;

        public decimal Amount()
        {
            if (_seniority < 2 || _monthsDisabled > 12 || _isPartTime)
            {
                return 0;
            }

            return _baseAmount * 0.1m;
        }
    }
}
