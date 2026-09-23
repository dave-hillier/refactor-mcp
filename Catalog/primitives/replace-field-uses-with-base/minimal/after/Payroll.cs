namespace Staff
{
    public class Payroll
    {
        public string Slip(Employee employee) => employee.Badge() + ": " + employee.Salary;
    }
}
