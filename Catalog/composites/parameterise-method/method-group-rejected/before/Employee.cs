using System;

namespace Payroll
{
    public class Employee
    {
        private decimal _salary;

        public void TenPercentRaise()
        {
            _salary *= 1.10m;
        }

        public void FivePercentRaise()
        {
            _salary *= 1.05m;
        }

        public Action Later()
        {
            TenPercentRaise();
            return FivePercentRaise;
        }
    }
}
