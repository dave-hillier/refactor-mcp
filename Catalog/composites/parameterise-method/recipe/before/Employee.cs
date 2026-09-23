namespace Payroll
{
    public class Employee
    {
        private decimal _salary;

        public Employee(decimal salary)
        {
            _salary = salary;
        }

        public void TenPercentRaise()
        {
            _salary *= /*[*/1.10m/*]*/;
        }

        public void FivePercentRaise()
        {
            _salary *= 1.05m;
        }

        public decimal Salary()
        {
            return _salary;
        }
    }
}
