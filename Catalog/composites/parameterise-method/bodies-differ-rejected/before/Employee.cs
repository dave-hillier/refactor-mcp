namespace Payroll
{
    public class Employee
    {
        private decimal _salary;

        public void Raise()
        {
            _salary *= 1.1m;
        }

        public void Cut()
        {
            _salary /= 1.1m;
        }
    }
}
