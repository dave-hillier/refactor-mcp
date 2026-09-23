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
            if (date < _summerStart || date > _summerEnd)
            {
                return quantity * _winterRate + _winterServiceCharge;
            }
            else
            {
                /*[*/return quantity * _summerRate;/*]*/
            }
        }
    }
}
