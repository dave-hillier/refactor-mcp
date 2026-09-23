namespace Staff
{
    public class Payroll
    {
        private bool _isDead;
        private bool _isSeparated;
        private bool _isRetired;

        public decimal PayAmount()
        {
            if (_isDead)
            {
                return DeadAmount();
            }

            if (_isSeparated)
            {
                return SeparatedAmount();
            }

            if (_isRetired)
            {
                return RetiredAmount();
            }

            return NormalPayAmount();
        }

        private decimal DeadAmount() => 0m;

        private decimal SeparatedAmount() => 100m;

        private decimal RetiredAmount() => 200m;

        private decimal NormalPayAmount() => 300m;
    }
}
