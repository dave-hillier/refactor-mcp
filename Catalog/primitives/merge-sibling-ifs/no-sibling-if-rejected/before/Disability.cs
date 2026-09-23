namespace Staff
{
    public class Disability
    {
        private int _seniority;
        private int _monthsDisabled;

        public decimal Amount()
        {
            /*^*/if (_seniority < 2)
            {
                return 0;
            }

            if (_monthsDisabled > 12)
            {
                return 1;
            }

            return 2;
        }
    }
}
