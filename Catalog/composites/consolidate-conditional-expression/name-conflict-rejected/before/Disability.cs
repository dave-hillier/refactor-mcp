namespace Staff
{
    public class Disability
    {
        private int _seniority;
        private bool _isPartTime;

        public bool IsNotEligible { get; set; }

        public decimal Amount()
        {
            /*^*/if (_seniority < 2)
            {
                return 0;
            }

            if (_isPartTime)
            {
                return 0;
            }

            return 10;
        }
    }
}
