using System;

namespace Shop
{
    public class Account
    {
        private decimal _balance;

        public decimal Balance
        {
            get => _balance;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                _balance = value;
            }
        }
    }
}
