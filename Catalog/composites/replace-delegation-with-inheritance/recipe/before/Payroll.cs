namespace Staff
{
    public class Payroll
    {
        public string Slip(Employee employee)
        {
            employee.Name = "Ann Lee";
            return employee.LastName() + ": " + employee.Salary + " " + employee.Badge();
        }
    }
}
