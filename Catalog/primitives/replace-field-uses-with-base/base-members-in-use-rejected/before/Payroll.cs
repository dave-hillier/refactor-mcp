namespace Staff
{
    public class Payroll
    {
        public string Slip(Employee employee) => employee.LastName() + ": " + employee.Badge();
    }
}
