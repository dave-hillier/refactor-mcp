using System;

namespace Billing
{
    public class Tariff
    {
        private DateTime _summerStart;
        private DateTime _summerEnd;
        private decimal _winterRate;
        private decimal _summerRate;

        public decimal Charge(DateTime date, int quantity)
        {
            decimal charge;
            if (date < _summerStart || date > _summerEnd)
                charge = quantity * _winterRate;
            else
                /*[*/charge = quantity * _summerRate;/*]*/
            return charge;
        }
    }
}
