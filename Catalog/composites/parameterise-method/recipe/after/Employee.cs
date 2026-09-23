namespace Payroll
{
    public class Employee
    {
        private decimal _salary;

        public Employee(decimal salary)
        {
            _salary = salary;
        }

        public void Raise(decimal factor)
        {
            _salary *= factor;
        }

        public decimal Salary()
        {
            return _salary;
        }
    }
}
