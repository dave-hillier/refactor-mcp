namespace Staff
{
    public class Payroll
    {
        private bool _isDead;
        private bool _isSeparated;
        private bool _isRetired;

        public decimal PayAmount()
        {
            /*^*/if (_isDead)
            {
                return DeadAmount();
            }
            else
            {
                if (_isSeparated)
                {
                    return SeparatedAmount();
                }
                else
                {
                    if (_isRetired)
                    {
                        return RetiredAmount();
                    }
                    else
                    {
                        return NormalPayAmount();
                    }
                }
            }
        }

        private decimal DeadAmount() => 0m;

        private decimal SeparatedAmount() => 100m;

        private decimal RetiredAmount() => 200m;

        private decimal NormalPayAmount() => 300m;
    }
}
