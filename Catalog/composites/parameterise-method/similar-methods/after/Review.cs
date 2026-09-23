namespace Payroll
{
    public class Review
    {
        public void Apply(Employee employee, bool outstanding)
        {
            if (outstanding)
                employee.Raise(1.10m);
            else
                employee.Raise(1.05m);
        }
    }
}
