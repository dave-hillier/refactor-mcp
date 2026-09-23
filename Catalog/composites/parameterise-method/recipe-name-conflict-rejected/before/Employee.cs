namespace Payroll
{
    public class Employee
    {
        private decimal _salary;

        public void TenPercentRaise(decimal bonus)
        {
            _salary = _salary * /*[*/1.10m/*]*/ + bonus;
        }

        public void FivePercentRaise(decimal bonus)
        {
            _salary = _salary * 1.05m + bonus;
        }
    }
}
