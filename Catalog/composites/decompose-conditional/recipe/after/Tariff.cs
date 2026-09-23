using System;

namespace Billing
{
    public class Tariff
    {
        private readonly DateTime _summerStart;
        private readonly DateTime _summerEnd;
        private readonly decimal _winterRate;
        private readonly decimal _winterServiceCharge;
        private readonly decimal _summerRate;

        public Tariff(DateTime summerStart, DateTime summerEnd, decimal winterRate, decimal winterServiceCharge, decimal summerRate)
        {
            _summerStart = summerStart;
            _summerEnd = summerEnd;
            _winterRate = winterRate;
            _winterServiceCharge = winterServiceCharge;
            _summerRate = summerRate;
        }

        public decimal Charge(DateTime date, int quantity)
        {
            if (NotSummer(date))
            {
                return WinterCharge(quantity);
            }
            else
            {
                return SummerCharge(quantity);
            }
        }

        private bool NotSummer(DateTime date)
        {
            return date < _summerStart || date > _summerEnd;
        }

        private decimal WinterCharge(int quantity)
        {
            return quantity * _winterRate + _winterServiceCharge;
        }

        private decimal SummerCharge(int quantity)
        {
            return quantity * _summerRate;
        }
    }
}
