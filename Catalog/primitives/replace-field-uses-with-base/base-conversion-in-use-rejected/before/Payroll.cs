namespace Staff
{
    public class Payroll
    {
        public string Slip(Employee employee) => Describe(employee) + ": " + employee.Badge();

        private static string Describe(Person person) => person.GetType().Name;
    }
}
