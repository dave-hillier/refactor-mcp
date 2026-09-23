namespace Staff
{
    public class Disability
    {
        private int _seniority;

        public decimal Amount()
        {
            if (_seniority < 2)
            {
                return 0;
            }

            /*^*/return 1;
        }
    }
}
