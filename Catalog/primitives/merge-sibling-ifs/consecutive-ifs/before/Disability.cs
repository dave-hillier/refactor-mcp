namespace Staff
{
    public class Disability
    {
        private int _seniority;
        private int _monthsDisabled;
        private decimal _baseAmount;

        public decimal Amount()
        {
            /*^*/if (_seniority < 2)
            {
                return 0;
            }

            if (_monthsDisabled > 12)
            {
                return 0;
            }

            return _baseAmount * 0.1m;
        }
    }
}
